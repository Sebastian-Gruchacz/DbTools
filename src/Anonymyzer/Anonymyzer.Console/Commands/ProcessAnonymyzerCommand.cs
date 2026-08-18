namespace Anonymyzer.Console.Commands;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.InternalInterfaces;
using Anonymyzer.Console.Planning;
using Anonymyzer.Configuration.Safety;
using Newtonsoft.Json;

internal sealed class ProcessAnonymyzerCommand
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IGeneratorsProvider _generatorsProvider;
    private readonly IEngineFactory _engineFactory;
    private readonly ICommandLogger _logger;
    private readonly DetachedCopySafetyValidator _safetyValidator;

    public ProcessAnonymyzerCommand(
        IDbConnectionFactory dbConnectionFactory,
        IEngineFactory engineFactory,
        IGeneratorsProvider generatorsProvider,
        ICommandLogger logger,
        DetachedCopySafetyValidator safetyValidator)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
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
        var planner = new AnonymizationExecutionPlanner(_generatorsProvider.GetAllGenerators());
        AnonymizationExecutionPlan plan = planner.Build(configuration);

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
        IAnonymyzerEngine engine = _engineFactory.CreateEngine(configuration.Database.DatabaseEngine, connection)
            ?? throw new InvalidOperationException(
                $"Database engine '{configuration.Database.DatabaseEngine}' is not installed.");
        ExecutionPlanDatabaseInspection inspection = new ExecutionPlanDatabaseInspector()
            .Inspect(configuration, plan, engine);
        ExecutionWriteSliceAssessment writeSlice = new ExecutionWriteSliceValidator()
            .Assess(plan, inspection);
        _logger.Info(
            $"Dry-run passed for {configuration.Database.DatabaseEngine} database " +
            $"'{configuration.Database.DatabaseName}', marker {marker.MarkerId:D}. No data was modified.");
        foreach (string line in ExecutionPlanFormatter.Format(plan, inspection, writeSlice))
        {
            _logger.Info(line);
        }

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

}

internal sealed class ProcessAnonymyzerCommandParameters : DbParameters
{
    public string ConfigurationFilePath { get; set; } = string.Empty;

    public Guid ExpectedMarkerId { get; set; }

    public bool DryRun { get; set; }
}
