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
        EnsureReportDoesNotOverwriteConfiguration(parameters);
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
        foreach (string line in ExecutionPlanFormatter.Format(plan, inspection, writeSlice))
        {
            _logger.Info(line);
        }

        if (parameters.DryRun)
        {
            _logger.Info(
                $"Dry-run passed for {configuration.Database.DatabaseEngine} database " +
                $"'{configuration.Database.DatabaseName}', marker {marker.MarkerId:D}. No data was modified.");
            return (int)ErrorCodes.Success;
        }

        if (!parameters.Execute || !writeSlice.IsSupported)
        {
            _logger.Error($"Execution refused: {writeSlice.Message}.");
            return (int)ErrorCodes.ConfigurationError;
        }

        var store = new DatabaseExecutionRowStore(connection, configuration.Database.DatabaseEngine);
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        AnonymizationExecutionResult result = new AnonymizationExecutor(_generatorsProvider.GetAllGenerators())
            .ExecuteWithResultAsync(plan, writeSlice, store)
            .GetAwaiter()
            .GetResult();
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        _logger.Info(
            $"Execution completed on detached clone '{configuration.Database.DatabaseName}', " +
            $"marker {marker.MarkerId:D}. Updated {result.ProcessedRows:N0} row(s) " +
            $"in {result.CommittedBatches:N0} committed batch(es).");
        if (!string.IsNullOrWhiteSpace(parameters.ReportFilePath))
        {
            AnonymizationExecutionReport report = AnonymizationExecutionReport.Completed(
                startedAtUtc,
                completedAtUtc,
                Path.GetFullPath(parameters.ConfigurationFilePath),
                configuration.Database.DatabaseEngine,
                configuration.Database.DatabaseName,
                marker.MarkerId,
                plan,
                writeSlice,
                result);
            try
            {
                AnonymizationExecutionReportWriter.Write(parameters.ReportFilePath, report);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Anonymization completed and data was modified, but the execution report could not be written: " +
                    exception.Message,
                    exception);
            }

            _logger.Info($"Execution report written to '{Path.GetFullPath(parameters.ReportFilePath)}'.");
        }

        return (int)ErrorCodes.Success;
    }

    private static void EnsureReportDoesNotOverwriteConfiguration(ProcessAnonymyzerCommandParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.ReportFilePath))
        {
            return;
        }

        string configurationPath = Path.GetFullPath(parameters.ConfigurationFilePath);
        string reportPath = Path.GetFullPath(parameters.ReportFilePath);
        if (configurationPath.Equals(reportPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The execution report must not overwrite the configuration file.");
        }
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

    public bool Execute { get; set; }

    public string? ReportFilePath { get; set; }
}
