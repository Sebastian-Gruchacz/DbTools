namespace Anonymyzer.Console.Commands;

using System.Data;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Console.Safety;
using Newtonsoft.Json;

internal sealed class ProcessAnonymyzerCommand
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IGeneratorsProvider _generatorsProvider;
    private readonly ICommandLogger _logger;
    private readonly DetachedCopySafetyValidator _safetyValidator;

    public ProcessAnonymyzerCommand(
        IDbConnectionFactory dbConnectionFactory,
        IGeneratorsProvider generatorsProvider,
        ICommandLogger logger,
        DetachedCopySafetyValidator safetyValidator)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _generatorsProvider = generatorsProvider ?? throw new ArgumentNullException(nameof(generatorsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _safetyValidator = safetyValidator ?? throw new ArgumentNullException(nameof(safetyValidator));
    }

    public int Process(ProcessAnonymyzerCommandParameters parameters)
    {
        if (!parameters.DryRun)
        {
            _logger.Error("Data modification is not implemented. Use --dry-run to validate a detached clone.");
            return (int)ErrorCodes.ConfigurationError;
        }

        AnonymizationConfiguration configuration = LoadConfiguration(parameters.ConfigurationFilePath);
        ConfigurationValidator.EnsureValid(configuration);
        DetachedCopySafetyValidator.EnsureConfigurationDoesNotTargetMarker(configuration);
        ValidateGenerators(configuration);

        parameters.DatabaseEngine = configuration.Database.DatabaseEngine;
        parameters.DatabaseName = configuration.Database.DatabaseName;
        using IDbConnection connection = _dbConnectionFactory.CreateMainConnection(parameters)
            ?? throw new InvalidOperationException(
                $"Database engine '{configuration.Database.DatabaseEngine}' is not installed.");

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        DetachedCopyMarker marker = _safetyValidator.Validate(
            configuration.Database,
            parameters.ExpectedMarkerId,
            connection);
        int enabledTables = configuration.Tables.Count(table => table.Enabled);
        int enabledColumns = configuration.Tables.Sum(table => table.Columns.Count(column => column.Enabled));

        _logger.Info(
            $"Dry-run passed for {configuration.Database.DatabaseEngine} database " +
            $"'{configuration.Database.DatabaseName}', marker {marker.MarkerId:D}, " +
            $"{enabledTables} enabled table(s), {enabledColumns} enabled column(s). No data was modified.");
        return (int)ErrorCodes.Success;
    }

    private static AnonymizationConfiguration LoadConfiguration(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Configuration file path is required.");
        }

        string json = File.ReadAllText(Path.GetFullPath(path));
        return JsonConvert.DeserializeObject<AnonymizationConfiguration>(json)
            ?? throw new InvalidOperationException("Configuration file is empty.");
    }

    private void ValidateGenerators(AnonymizationConfiguration configuration)
    {
        Dictionary<(string Type, string Version), IGenerator> generators = _generatorsProvider.GetAllGenerators()
            .ToDictionary(
                generator => (generator.Descriptor.Type.ToUpperInvariant(), generator.Descriptor.Version),
                generator => generator);

        foreach (GeneratorProfileConfiguration profile in configuration.GeneratorProfiles)
        {
            if (!generators.TryGetValue((profile.GeneratorType.ToUpperInvariant(), profile.GeneratorVersion), out IGenerator? generator))
            {
                throw new InvalidOperationException(
                    $"Generator {profile.GeneratorType} {profile.GeneratorVersion} required by profile '{profile.Id}' is not installed.");
            }

            object options = generator.Configuration.Deserialize(profile.Options);
            IReadOnlyList<string> errors = generator.Configuration.Validate(options);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Generator profile '{profile.Id}' is invalid: {string.Join("; ", errors)}");
            }
        }
    }
}

internal sealed class ProcessAnonymyzerCommandParameters : DbParameters
{
    public string ConfigurationFilePath { get; set; } = string.Empty;

    public Guid ExpectedMarkerId { get; set; }

    public bool DryRun { get; set; }
}
