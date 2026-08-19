namespace Anonymyzer.Console.Commands;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
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
        EnsureOutputPathsAreDistinct(parameters);
        AnonymizationConfiguration configuration = LoadConfiguration(parameters.ConfigurationFilePath);
        ConfigurationValidator.EnsureValid(configuration);
        DetachedCopySafetyValidator.EnsureConfigurationDoesNotTargetMarker(configuration);
        IGenerator[] generators = _generatorsProvider.GetAllGenerators().ToArray();
        var planner = new AnonymizationExecutionPlanner(generators);
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

        AnonymizationExecutionCheckpoint? checkpoint = null;
        if (!string.IsNullOrWhiteSpace(parameters.CheckpointFilePath))
        {
            PrimaryKeyFingerprint.EnsureSecretIsValid(parameters.CheckpointFingerprintSecret);

            ExecutionResumeSafetyAssessment resumeSafety = new ExecutionResumeSafetyAssessor().Assess(plan);
            if (!resumeSafety.IsSupported)
            {
                throw new InvalidOperationException(
                    $"Checkpoint execution refused: {resumeSafety.Message}.");
            }

            IReadOnlyDictionary<string, string> replayDependencyFingerprints =
                ReplayDependencyFingerprint.Compute(
                    plan,
                    generators,
                    parameters.CheckpointFingerprintSecret!);

            AnonymizationExecutionCheckpoint expectedCheckpoint = AnonymizationExecutionCheckpoint.Create(
                parameters.ConfigurationFilePath,
                configuration.Database.DatabaseEngine,
                configuration.Database.DatabaseName,
                marker.MarkerId,
                plan,
                writeSlice,
                replayDependencyFingerprints);
            checkpoint = AnonymizationExecutionCheckpointStore.Load(parameters.CheckpointFilePath)
                ?? expectedCheckpoint;
            checkpoint.EnsureMatches(expectedCheckpoint);
            if (checkpoint.IsCompleted)
            {
                _logger.Info(
                    $"Checkpoint '{Path.GetFullPath(parameters.CheckpointFilePath)}' is already completed. " +
                    "No data was modified.");
                return (int)ErrorCodes.Success;
            }

            AnonymizationExecutionCheckpointStore.Write(parameters.CheckpointFilePath, checkpoint);
            if (checkpoint.ProcessedRows > 0)
            {
                _logger.Info(
                    $"Resuming after {checkpoint.ProcessedRows:N0} row(s) in " +
                    $"{checkpoint.CommittedBatches:N0} committed batch(es).");
            }
        }

        var postExecutionValidator = new PostExecutionDatabaseValidator();
        long rowCountBefore = postExecutionValidator.CountRows(
            connection,
            configuration.Database.DatabaseEngine,
            writeSlice.TargetTable!);
        ConstraintValidationResult constraintBaseline = postExecutionValidator.ValidateConstraints(
            connection,
            configuration.Database.DatabaseEngine,
            writeSlice.TargetTable!);
        if (constraintBaseline.Issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Pre-execution constraint validation failed; no data was modified: " +
                string.Join(" ", constraintBaseline.Issues));
        }

        var store = new DatabaseExecutionRowStore(connection, configuration.Database.DatabaseEngine);
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var executor = new AnonymizationExecutor(generators);
        AnonymizationExecutionResult result;
        if (checkpoint is null)
        {
            result = executor.ExecuteWithResultAsync(plan, writeSlice, store)
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            result = executor.ExecuteWithResumeAsync(
                    plan,
                    writeSlice,
                    store,
                    new AnonymizationExecutionResumeState(
                        checkpoint.ProcessedRows,
                        checkpoint.CommittedBatches,
                        checkpoint.LastPrimaryKeyHmacSha256,
                        parameters.CheckpointFingerprintSecret!)
                    {
                        ReplayDependencyHmacSha256 = checkpoint.ReplayDependencyHmacSha256
                    },
                    (progress, _) =>
                    {
                        checkpoint.Advance(progress, parameters.CheckpointFingerprintSecret!);
                        try
                        {
                            AnonymizationExecutionCheckpointStore.Write(
                                parameters.CheckpointFilePath!,
                                checkpoint);
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException(
                                "A batch was committed, but its checkpoint could not be written. " +
                                "Retry with the same checkpoint path to reproduce that batch safely: " +
                                exception.Message,
                                exception);
                        }

                        return Task.CompletedTask;
                    })
                .GetAwaiter()
                .GetResult();
        }
        PostExecutionValidationResult validation = ValidatePostExecution(
            connection,
            configuration,
            plan,
            engine,
            marker.MarkerId,
            writeSlice.TargetTable!,
            rowCountBefore,
            constraintBaseline.CheckedConstraints,
            postExecutionValidator);
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        _logger.Info(
            $"Execution completed on detached clone '{configuration.Database.DatabaseName}', " +
            $"marker {marker.MarkerId:D}. Updated {result.ProcessedRows:N0} row(s) " +
            $"in {result.CommittedBatches:N0} committed batch(es).");
        if (validation.Passed)
        {
            _logger.Info(
                $"Post-execution validation passed: marker and schema are valid, row count is " +
                $"{validation.RowCountAfter:N0}, checked {validation.CheckedConstraints:N0} constraint(s).");
        }

        if (checkpoint is not null && validation.Passed)
        {
            checkpoint.Complete(result, parameters.CheckpointFingerprintSecret!);
            try
            {
                AnonymizationExecutionCheckpointStore.Write(parameters.CheckpointFilePath!, checkpoint);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Anonymization and validation completed, but the checkpoint could not be marked complete. " +
                    "Retry with the same checkpoint path: " + exception.Message,
                    exception);
            }
        }

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
                result,
                validation);
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

        if (!validation.Passed)
        {
            _logger.Error(
                "Post-execution validation failed after data was modified. " +
                "The clone must not be released for use.");
            foreach (string issue in validation.Issues)
            {
                _logger.Error($"Validation issue: {issue}");
            }

            return (int)ErrorCodes.ConfigurationError;
        }

        return (int)ErrorCodes.Success;
    }

    private PostExecutionValidationResult ValidatePostExecution(
        IDbConnection connection,
        AnonymizationConfiguration configuration,
        AnonymizationExecutionPlan plan,
        IAnonymyzerEngine engine,
        Guid markerId,
        Anonymyzer.Base.Generation.GeneratorTableReference targetTable,
        long rowCountBefore,
        int expectedConstraintCount,
        PostExecutionDatabaseValidator validator)
    {
        var issues = new List<string>();
        bool markerValid = false;
        bool schemaValid = false;
        long? rowCountAfter = null;
        int checkedConstraints = 0;
        try
        {
            _safetyValidator.Validate(configuration.Database, markerId, connection);
            markerValid = true;
        }
        catch (Exception exception)
        {
            issues.Add("Detached-copy marker validation failed: " + exception.Message);
        }

        try
        {
            new ExecutionPlanDatabaseInspector().Inspect(configuration, plan, engine);
            schemaValid = true;
        }
        catch (Exception exception)
        {
            issues.Add("Schema validation failed: " + exception.Message);
        }

        try
        {
            rowCountAfter = validator.CountRows(
                connection,
                configuration.Database.DatabaseEngine,
                targetTable);
            if (rowCountAfter != rowCountBefore)
            {
                issues.Add(
                    $"Target row count changed from {rowCountBefore:N0} to {rowCountAfter:N0}.");
            }
        }
        catch (Exception exception)
        {
            issues.Add("Exact row-count validation failed: " + exception.Message);
        }

        try
        {
            ConstraintValidationResult constraints = validator.ValidateConstraints(
                connection,
                configuration.Database.DatabaseEngine,
                targetTable);
            checkedConstraints = constraints.CheckedConstraints;
            issues.AddRange(constraints.Issues);
            if (checkedConstraints != expectedConstraintCount)
            {
                issues.Add(
                    $"Target constraint count changed from {expectedConstraintCount:N0} " +
                    $"to {checkedConstraints:N0} during execution.");
            }
        }
        catch (Exception exception)
        {
            issues.Add("Constraint validation could not be completed: " + exception.Message);
        }

        return new PostExecutionValidationResult(
            issues.Count == 0 && markerValid && schemaValid && rowCountAfter == rowCountBefore,
            markerValid,
            schemaValid,
            rowCountBefore,
            rowCountAfter,
            checkedConstraints,
            issues);
    }

    private static void EnsureOutputPathsAreDistinct(ProcessAnonymyzerCommandParameters parameters)
    {
        string configurationPath = Path.GetFullPath(parameters.ConfigurationFilePath);
        string? reportPath = string.IsNullOrWhiteSpace(parameters.ReportFilePath)
            ? null
            : Path.GetFullPath(parameters.ReportFilePath);
        string? checkpointPath = string.IsNullOrWhiteSpace(parameters.CheckpointFilePath)
            ? null
            : Path.GetFullPath(parameters.CheckpointFilePath);
        if (reportPath is not null
            && configurationPath.Equals(reportPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The execution report must not overwrite the configuration file.");
        }

        if (checkpointPath is not null
            && configurationPath.Equals(checkpointPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The checkpoint must not overwrite the configuration file.");
        }

        if (reportPath is not null
            && checkpointPath is not null
            && reportPath.Equals(checkpointPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The report and checkpoint must use different files.");
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

    public string? CheckpointFilePath { get; set; }

    public string? CheckpointFingerprintSecret { get; set; }
}
