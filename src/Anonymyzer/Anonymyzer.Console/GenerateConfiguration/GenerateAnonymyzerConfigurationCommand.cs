namespace Anonymyzer.Console.GenerateConfiguration;

using System.Data;
using System.Text;
using System.Text.Json;

using Anonymyzer.Base;
using Anonymyzer.Console.CommandLibraryElements;
using Anonymyzer.Console.Configuration;

internal class GenerateAnonymyzerConfigurationCommand// : ICommand<GenerateAnonymyzerConfigurationCommandParameters>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IEngineFactory _engineFactory;
    private readonly ICommandLogger _logger;

    public GenerateAnonymyzerConfigurationCommand(IDbConnectionFactory dbConnectionFactory, IEngineFactory engineFactory,
        ICommandLogger logger)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public int Process(GenerateAnonymyzerConfigurationCommandParameters parameters)
    {
        IDbConnection? connection = _dbConnectionFactory.CreateConnection(parameters.DatabaseEngine, parameters.ConnectionString);
        if (connection is null)
        {
            _logger.Error(@"Could not connect to the DB.");
            return (int)ErrorCodes.ConfigurationError;
        }

        try
        {
            connection.Open();

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

            var tables = engine.ListTables(listSystemTables: false).ToArray();
            if (!tables.Any())
            {
                _logger.Warning(@"No tables returned for processing.");
                return (int)ErrorCodes.Ignored;
            }

            StreamWriter? stream = null;
            try
            {
                stream = new StreamWriter(path, Encoding.UTF8,
                    new FileStreamOptions()
                    {
                        Access = FileAccess.Write,
                        Mode = FileMode.OpenOrCreate,
                        Share = FileShare.None
                    });

                return ExportTablesForScripting(engine, tables, stream);

            }
            finally
            {
                stream?.Close();
                stream?.Dispose();
            }
        }
        finally
        {
            connection.Close();
        }
    }

    private string? CheckCreateOutputFile(GenerateAnonymyzerConfigurationCommandParameters parameters)
    {
        var fIfo = new FileInfo(parameters.ConfigurationFilePath);
        if (fIfo.Exists)
        {
            if (!parameters.DoOverride)
            {
                _logger.Error(@$"Output file already exists at: {fIfo.FullName}");
                return null;
            }

            fIfo.Delete();
        }

        if (!fIfo.Directory!.Exists)
        {
            Directory.CreateDirectory(fIfo.Directory.FullName);
        }

        return parameters.ConfigurationFilePath.EndsWith(@".json")
            ? parameters.ConfigurationFilePath
            : parameters.ConfigurationFilePath + @".json";
    }

    private int ExportTablesForScripting(IAnonymyzerEngine engine, ITableInfo[] tables, StreamWriter stream)
    {
        List<TableProcessingOptions> outputConfigs = new();

        foreach (ITableInfo tableInfo in tables)
        {
            var tableConfig = CreateConfigForTable(engine, tableInfo);
            outputConfigs.Add(tableConfig);
        }

        var config = new AnonymyzationConfiguration()
        {
            Tables = outputConfigs.ToArray()
        };

        JsonSerializer.Serialize(stream.BaseStream, config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        return (int)ErrorCodes.Success;
    }

    private TableProcessingOptions CreateConfigForTable(IAnonymyzerEngine engine, ITableInfo tableInfo)
    {
        var config = TableProcessingOptions.DefaultForTable(tableInfo.Name);

        var columns = engine.ListTextColumns(tableInfo);
        foreach (IColumnInfo column in columns)
        {
            var columnInfo = new ColumnProcessingOptions
            {
                ColumnName = column.Name,
                DataType = column.DataType.ToString(),
                MaxLength = column.MaxLength,
                Unicode = column.IsUnicode,
                Ignore = true, // TODO: use "AI"
            };

            config.Columns.Add(columnInfo);
        }

        return config;
    }
}