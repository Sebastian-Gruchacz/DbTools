namespace Anonymyzer.Console.GenerateConfiguration;

using System.Data;
using System.Text;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Base.LanguagePacks;
using Anonymyzer.Configuration;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Configuration.Safety;
using Newtonsoft.Json;

internal class GenerateAnonymyzerConfigurationCommand// : ICommand<GenerateAnonymyzerConfigurationCommandParameters>
{
    private const string DEFAULT = @"Default";
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ColumnConfigurationBuilder _columnConfigurationBuilder;
    private readonly IEngineFactory _engineFactory;
    private readonly IGeneratorsProvider _generatorsProvider;
    private readonly LanguagePackCatalog _languagePacks;
    private readonly ICommandLogger _logger;
    private readonly DetachedCopySafetyValidator _safetyValidator;
    private readonly JsonSerializer _serializer = new JsonSerializer()
    {
        Formatting = Formatting.Indented
    };

    public GenerateAnonymyzerConfigurationCommand(
        IDbConnectionFactory dbConnectionFactory,
        ColumnCandidateDetector candidateDetector,
        IEngineFactory engineFactory,
        IGeneratorsProvider generatorsProvider,
        LanguagePackCatalog languagePacks,
        ICommandLogger logger,
        DetachedCopySafetyValidator safetyValidator)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _columnConfigurationBuilder = new ColumnConfigurationBuilder(
            candidateDetector ?? throw new ArgumentNullException(nameof(candidateDetector)));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _generatorsProvider = generatorsProvider ?? throw new ArgumentNullException(nameof(generatorsProvider));
        _languagePacks = languagePacks ?? throw new ArgumentNullException(nameof(languagePacks));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safetyValidator = safetyValidator ?? throw new ArgumentNullException(nameof(safetyValidator));
    }

    public int Process(GenerateAnonymyzerConfigurationCommandParameters parameters)
    {
        IDbConnection? connection = _dbConnectionFactory.CreateMainConnection(parameters);
        if (connection is null)
        {
            _logger.Error(@"Could not connect to the DB.");
            return (int)ErrorCodes.ConfigurationError;
        }

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var expectedTarget = new DatabaseTargetConfiguration
            {
                DatabaseEngine = parameters.DatabaseEngine,
                DatabaseName = parameters.DatabaseName,
                DetachedCopyMarkerId = parameters.ExpectedMarkerId.ToString("D")
            };
            DetachedCopyMarker marker = _safetyValidator.Validate(
                expectedTarget,
                parameters.ExpectedMarkerId,
                connection);

            IAnonymyzerEngine? engine = _engineFactory.CreateEngine(parameters.DatabaseEngine, connection);
            if (engine is null)
            {
                _logger.Error($@"Could not find anonymyzation engine for ""{parameters.DatabaseEngine}"".");
                return (int)ErrorCodes.ConfigurationError;
            }

            string? path = CheckCreateOutputFile(parameters);
            if (path is null)
            {
                return (int)ErrorCodes.ConfigurationError;
            }

            var tables = engine.ListTables(listSystemTables: false)
                .Where(table => !DetachedCopySafetyValidator.IsMarkerTable(
                    parameters.DatabaseEngine,
                    table.SchemaName,
                    table.Name))
                .ToArray();
            if (!tables.Any())
            {
                _logger.Warning(@"No tables returned for processing.");
                return (int)ErrorCodes.Ignored;
            }

            using var stream = new StreamWriter(
                new FileStream(
                    path,
                    parameters.DoOverride ? FileMode.Create : FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return ExportTablesForScripting(engine, tables, stream, parameters, marker);
        }
        finally
        {
            connection.Close();
            connection.Dispose();
        }
    }

    private string? CheckCreateOutputFile(GenerateAnonymyzerConfigurationCommandParameters parameters)
    {
        string path = parameters.ConfigurationFilePath.EndsWith(@".json", StringComparison.OrdinalIgnoreCase)
            ? parameters.ConfigurationFilePath
            : parameters.ConfigurationFilePath + @".json";
        var fIfo = new FileInfo(path);
        if (fIfo.Exists && !parameters.DoOverride)
        {
            _logger.Error(@$"Output file already exists at: {fIfo.FullName}");
            return null;
        }

        if (!fIfo.Directory!.Exists)
        {
            Directory.CreateDirectory(fIfo.Directory.FullName);
        }

        return fIfo.FullName;
    }

    private int ExportTablesForScripting(IAnonymyzerEngine engine, ITableInfo[] tables, StreamWriter stream,
        DbParameters parameters, DetachedCopyMarker marker)
    {
        List<TableProcessingOptions> outputConfigs = new();

        foreach (ITableInfo tableInfo in tables)
        {
            var tableConfig = _columnConfigurationBuilder.CreateTable(engine, tableInfo);

            // Only write tables with at least one anonymyzable column TODO: configurable?
            if (tableConfig.Columns.Any())
            {
                outputConfigs.Add(tableConfig);
            }
        }

        var config = new AnonymizationConfiguration
        {
            Database = new DatabaseTargetConfiguration
            {
                DatabaseEngine = parameters.DatabaseEngine,
                DatabaseName = parameters.DatabaseName,
                DetachedCopyMarkerId = marker.MarkerId.ToString("D")
            },
            GeneratorProfiles = BuildDefaultGeneratorProfiles(),
            Tables = outputConfigs
        };

        _serializer.Serialize(stream, config);

        return (int)ErrorCodes.Success;
    }

    private List<GeneratorProfileConfiguration> BuildDefaultGeneratorProfiles()
    {
        HashSet<string> localizedGeneratorTypes = _languagePacks.Profiles
            .Select(item => item.Profile.GeneratorType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<GeneratorProfileConfiguration> generatorProfiles = _generatorsProvider.GetAllGenerators()
            .Where(generator => !localizedGeneratorTypes.Contains(generator.Descriptor.Type))
            .Select(generator => new GeneratorProfileConfiguration
            {
                Id = $"{generator.Descriptor.Type}:{DEFAULT}",
                DisplayName = $"{generator.Descriptor.DisplayName} ({DEFAULT})",
                GeneratorType = generator.Descriptor.Type,
                GeneratorVersion = generator.Descriptor.Version,
                Origin = "Built-in",
                Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
            });
        IEnumerable<GeneratorProfileConfiguration> languageProfiles = _languagePacks.Profiles.Select(item =>
            new GeneratorProfileConfiguration
            {
                Id = item.Profile.Id,
                DisplayName = item.Profile.DisplayName,
                GeneratorType = item.Profile.GeneratorType,
                GeneratorVersion = item.Profile.GeneratorVersion,
                Locale = item.Profile.Locale,
                Origin = $"Language pack: {item.Pack.Descriptor.DisplayName} {item.Pack.Descriptor.Version}",
                Options = (Newtonsoft.Json.Linq.JObject)item.Profile.Options.DeepClone()
            });
        return generatorProfiles.Concat(languageProfiles).ToList();
    }

}
