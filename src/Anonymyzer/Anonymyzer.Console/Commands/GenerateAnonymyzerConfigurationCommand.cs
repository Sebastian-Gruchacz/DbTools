namespace Anonymyzer.Console.GenerateConfiguration;

using System.Data;
using System.Text;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.Commands;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Console.Safety;
using Newtonsoft.Json;

internal class GenerateAnonymyzerConfigurationCommand// : ICommand<GenerateAnonymyzerConfigurationCommandParameters>
{
    private const string DEFAULT = @"Default";
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ColumnCandidateDetector _candidateDetector;
    private readonly IEngineFactory _engineFactory;
    private readonly IGeneratorsProvider _generatorsProvider;
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
        ICommandLogger logger,
        DetachedCopySafetyValidator safetyValidator)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _candidateDetector = candidateDetector ?? throw new ArgumentNullException(nameof(candidateDetector));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _generatorsProvider = generatorsProvider ?? throw new ArgumentNullException(nameof(generatorsProvider));
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
            var tableConfig = CreateConfigForTable(engine, tableInfo);

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
        return _generatorsProvider.GetAllGenerators()
            .Select(generator => new GeneratorProfileConfiguration
            {
                Id = $"{generator.Descriptor.Type}:{DEFAULT}",
                DisplayName = $"{generator.Descriptor.DisplayName} ({DEFAULT})",
                GeneratorType = generator.Descriptor.Type,
                GeneratorVersion = generator.Descriptor.Version,
                Options = generator.Configuration.Serialize(generator.Configuration.CreateDefault())
            })
            .ToList();
    }

    private TableProcessingOptions CreateConfigForTable(IAnonymyzerEngine engine, ITableInfo tableInfo)
    {
        var config = TableProcessingOptions.DefaultForTable(tableInfo.Name, tableInfo.SchemaName);

        var columns = engine.ListTextColumns(tableInfo);
        int ordinal = 0;
        foreach (IColumnInfo column in columns)
        {
            ordinal++;
            // TODO: for now only setting text, non-PK fields
            if (column.IsPartOfThePrimaryKey || column.DataType != DbDataType.Text)
            {
                continue;
            }

            var columnInfo = new ColumnProcessingOptions
            {
                Ordinal = ordinal,
                ColumnName = column.Name,
                DataType = column.DataType.ToString(),
                MaxLength = column.MaxLength,
                Unicode = column.IsUnicodeText,

                Enabled = false,
                Detection = _candidateDetector.Detect(column.Name),
                Generator = new ColumnGeneratorConfiguration
                {
                    GeneratorType = "TextShuffler",
                    GeneratorVersion = "1.0.0",
                    ProfileId = $"TextShuffler:{DEFAULT}"
                }
            };

            config.Columns.Add(columnInfo);
        }

        return config;
    }
}
