namespace Anonymyzer.Console.Commands;

using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.InternalInterfaces;

internal class ProcessAnonymyzerCommand // : ICommand<ProcessAnonymyzerCommandParameters>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IEngineFactory _engineFactory;
    private readonly IGeneratorsProvider _generatorsProvider;
    private readonly ICommandLogger _logger;

    public ProcessAnonymyzerCommand(IDbConnectionFactory dbConnectionFactory, IEngineFactory engineFactory,
        IGeneratorsProvider generatorsProvider, ICommandLogger logger)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _generatorsProvider = generatorsProvider ?? throw new ArgumentNullException(nameof(generatorsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // TODO: implementation & configuration
    public int Process(ProcessAnonymyzerCommandParameters parameters)
    {
        //var configFile = ;

        //AnonymizationConfiguration config;

        // The detached-copy connection must come from runtime arguments or a secret provider.
        // It must never be read from the anonymyzation configuration file.
        //var dbConnection = _dbConnectionFactory.CreateMainConnection(parameters);

        //var engine = _engineFactory.CreateEngine(config.Database.DatabaseEngine, dbConnection);

        // 1. check-build all generators, using global settings

        // 2. run through all tables


        // 2a. build all generator functions for all columns using global generators & local configurations (if any)

        // 2a. Disable indexes

        // 2b. run all rows, applying generators

        // 3c Re-enable / recalculate indexes


        return (int)ErrorCodes.Success;
    }
}

internal class ProcessAnonymyzerCommandParameters
{
    /// <summary>
    /// Gets or sets path to the generated anonymyzer configuration file
    /// </summary>
    public string ConfigurationFilePath { get; set; } = string.Empty;
}
