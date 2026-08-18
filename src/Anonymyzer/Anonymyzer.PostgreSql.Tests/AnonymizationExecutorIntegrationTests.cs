namespace Anonymyzer.PostgreSql.Tests;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.Console.Planning;
using Anonymyzer.Generators.Person;
using Anonymyzer.Generators.Simple;
using Anonymyzer.LanguagePack.Polish;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;
using Microsoft.Data.SqlClient;
using Npgsql;

public sealed class AnonymizationExecutorIntegrationTests
{
    [Fact]
    public async Task ExecutesPersonIdentityOnIsolatedSqlServerFixture()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_SQLSERVER_CONNECTION", "SQL Server");
        string tableName = "__AnonymyzerExecutor_" + Guid.NewGuid().ToString("N")[..12];
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        DetachedCopyMarker marker = ValidateClone("SqlServer", connection);
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE [dbo].[{tableName}] (" +
            "[Id] int NOT NULL PRIMARY KEY, [FirstName] nvarchar(100) NULL, " +
            "[LastName] nvarchar(100) NULL, [Gender] nvarchar(20) NULL, [Email] nvarchar(200) NULL); " +
            $"INSERT INTO [dbo].[{tableName}] ([Id], [FirstName], [LastName], [Gender], [Email]) VALUES " +
            "(1, N'Original A', N'Original A', N'Unknown', N'a@old.invalid'), " +
            "(2, N'Original B', N'Original B', N'Unknown', N'b@old.invalid'), " +
            "(3, NULL, NULL, NULL, NULL);");

        try
        {
            long processed = await ExecuteFixtureAsync(
                connection,
                new SqlServerAnonymyzerEngine(connection),
                "SqlServer",
                "dbo",
                tableName,
                marker);

            Assert.Equal(3, processed);
            Assert.Equal(3, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM [dbo].[{tableName}] " +
                "WHERE [FirstName] IS NOT NULL AND [LastName] IS NOT NULL " +
                "AND [Email] LIKE N'%@example.invalid';"));
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE [dbo].[{tableName}];");
        }
    }

    [Fact]
    public async Task ExecutesPersonIdentityOnIsolatedPostgreSqlFixture()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_POSTGRES_CONNECTION", "PostgreSQL");
        string tableName = "__anonymyzer_executor_" + Guid.NewGuid().ToString("N")[..12];
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        DetachedCopyMarker marker = ValidateClone("PostgreSql", connection);
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE public.\"{tableName}\" (" +
            "\"Id\" integer PRIMARY KEY, \"FirstName\" varchar(100), " +
            "\"LastName\" varchar(100), \"Gender\" varchar(20), \"Email\" varchar(200)); " +
            $"INSERT INTO public.\"{tableName}\" (\"Id\", \"FirstName\", \"LastName\", \"Gender\", \"Email\") VALUES " +
            "(1, 'Original A', 'Original A', 'Unknown', 'a@old.invalid'), " +
            "(2, 'Original B', 'Original B', 'Unknown', 'b@old.invalid'), " +
            "(3, NULL, NULL, NULL, NULL);");

        try
        {
            long processed = await ExecuteFixtureAsync(
                connection,
                new PostgreSqlAnonymyzerEngine(connection),
                "PostgreSql",
                "public",
                tableName,
                marker);

            Assert.Equal(3, processed);
            Assert.Equal(3, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM public.\"{tableName}\" " +
                "WHERE \"FirstName\" IS NOT NULL AND \"LastName\" IS NOT NULL " +
                "AND \"Email\" LIKE '%@example.invalid';"));
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE public.\"{tableName}\";");
        }
    }

    [Fact]
    public async Task ExecutesTextShufflerOnIsolatedPostgreSqlFixture()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_POSTGRES_CONNECTION", "PostgreSQL");
        string tableName = "__anonymyzer_shuffle_" + Guid.NewGuid().ToString("N")[..12];
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        DetachedCopyMarker marker = ValidateClone("PostgreSql", connection);
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE public.\"{tableName}\" (\"Id\" integer PRIMARY KEY, \"Name\" varchar(100)); " +
            $"INSERT INTO public.\"{tableName}\" (\"Id\", \"Name\") VALUES " +
            "(1, 'Ada'), (2, 'Grace'), (3, 'Margaret');");

        try
        {
            var generator = new ShufflingTextGenerator();
            AnonymizationConfiguration configuration = CreateShufflerConfiguration(
                generator,
                connection.Database,
                marker,
                tableName);
            long processed = await ExecutePlanAsync(
                connection,
                new PostgreSqlAnonymyzerEngine(connection),
                "PostgreSql",
                configuration,
                [generator]);

            Assert.Equal(3, processed);
            Assert.Equal(3, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM public.\"{tableName}\" WHERE \"Name\" IN ('Ada', 'Grace', 'Margaret');"));
            Assert.Equal(3, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(DISTINCT \"Name\") FROM public.\"{tableName}\";"));
            Assert.True(ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM public.\"{tableName}\" " +
                "WHERE (\"Id\" = 1 AND \"Name\" <> 'Ada') " +
                "OR (\"Id\" = 2 AND \"Name\" <> 'Grace') " +
                "OR (\"Id\" = 3 AND \"Name\" <> 'Margaret');") > 0);
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE public.\"{tableName}\";");
        }
    }

    private static async Task<long> ExecuteFixtureAsync(
        IDbConnection connection,
        IAnonymyzerEngine engine,
        string databaseEngine,
        string schemaName,
        string tableName,
        DetachedCopyMarker marker)
    {
        var generator = new PersonIdentityGenerator([new PolishPersonLocaleDataProvider()]);
        AnonymizationConfiguration configuration = CreateConfiguration(
            generator,
            databaseEngine,
            connection.Database,
            marker,
            schemaName,
            tableName);
        return await ExecutePlanAsync(connection, engine, databaseEngine, configuration, [generator]);
    }

    private static async Task<long> ExecutePlanAsync(
        IDbConnection connection,
        IAnonymyzerEngine engine,
        string databaseEngine,
        AnonymizationConfiguration configuration,
        IReadOnlyList<IGenerator> generators)
    {
        AnonymizationExecutionPlan plan = new AnonymizationExecutionPlanner(generators)
            .Build(configuration, batchSize: 2);
        ExecutionPlanDatabaseInspection inspection = new ExecutionPlanDatabaseInspector()
            .Inspect(configuration, plan, engine);
        ExecutionWriteSliceAssessment writeSlice = new ExecutionWriteSliceValidator()
            .Assess(plan, inspection);
        Assert.True(writeSlice.IsSupported, writeSlice.Message);

        return await new AnonymizationExecutor(generators).ExecuteAsync(
            plan,
            writeSlice,
            new DatabaseExecutionRowStore(connection, databaseEngine),
            TestContext.Current.CancellationToken);
    }

    private static AnonymizationConfiguration CreateShufflerConfiguration(
        ShufflingTextGenerator generator,
        string databaseName,
        DetachedCopyMarker marker,
        string tableName)
    {
        const string profileId = "shuffle-fixture";
        return new AnonymizationConfiguration
        {
            Database = new DatabaseTargetConfiguration
            {
                DatabaseEngine = "PostgreSql",
                DatabaseName = databaseName,
                DetachedCopyMarkerId = marker.MarkerId.ToString("D")
            },
            GeneratorProfiles =
            {
                new GeneratorProfileConfiguration
                {
                    Id = profileId,
                    GeneratorType = generator.Descriptor.Type,
                    GeneratorVersion = generator.Descriptor.Version,
                    Options = generator.Configuration.Serialize(new ShufflingTextGeneratorConfiguration
                    {
                        Seed = 42,
                        MinimumPopulation = 2,
                        PreserveNulls = true
                    })
                }
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = tableName,
                    Enabled = true,
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            Ordinal = 2,
                            ColumnName = "Name",
                            DataType = DbDataType.Text.ToString(),
                            MaxLength = 100,
                            Unicode = true,
                            Enabled = true,
                            Generator = new ColumnGeneratorConfiguration
                            {
                                GeneratorType = generator.Descriptor.Type,
                                GeneratorVersion = generator.Descriptor.Version,
                                ProfileId = profileId
                            }
                        }
                    }
                }
            }
        };
    }

    private static AnonymizationConfiguration CreateConfiguration(
        PersonIdentityGenerator generator,
        string databaseEngine,
        string databaseName,
        DetachedCopyMarker marker,
        string schemaName,
        string tableName)
    {
        const string groupId = "identity";
        var profile = new GeneratorProfileConfiguration
        {
            Id = "person-fixture",
            GeneratorType = generator.Descriptor.Type,
            GeneratorVersion = generator.Descriptor.Version,
            Options = generator.Configuration.Serialize(new PersonIdentityGeneratorConfiguration
            {
                Seed = 812,
                Locale = "pl-PL",
                EmailDomain = "example.invalid"
            })
        };
        var table = new TableProcessingOptions
        {
            SchemaName = schemaName,
            TableName = tableName,
            Enabled = true,
            Columns =
            {
                TextColumn(2, "FirstName", 100, groupId),
                TextColumn(3, "LastName", 100, groupId),
                TextColumn(4, "Gender", 20, groupId),
                TextColumn(5, "Email", 200, groupId)
            },
            GenerationGroups =
            {
                new GenerationGroupConfiguration
                {
                    Id = groupId,
                    GeneratorType = generator.Descriptor.Type,
                    GeneratorVersion = generator.Descriptor.Version,
                    ProfileId = profile.Id,
                    Bindings = new Dictionary<string, string>
                    {
                        [PersonIdentityGenerator.FirstNameOutput] = "FirstName",
                        [PersonIdentityGenerator.LastNameOutput] = "LastName",
                        [PersonIdentityGenerator.GenderOutput] = "Gender",
                        [PersonIdentityGenerator.EmailOutput] = "Email"
                    }
                }
            }
        };
        return new AnonymizationConfiguration
        {
            Database = new DatabaseTargetConfiguration
            {
                DatabaseEngine = databaseEngine,
                DatabaseName = databaseName,
                DetachedCopyMarkerId = marker.MarkerId.ToString("D")
            },
            GeneratorProfiles = { profile },
            Tables = { table }
        };
    }

    private static ColumnProcessingOptions TextColumn(
        int ordinal,
        string name,
        int maxLength,
        string groupId) => new()
    {
        Ordinal = ordinal,
        ColumnName = name,
        DataType = DbDataType.Text.ToString(),
        MaxLength = maxLength,
        Unicode = true,
        Enabled = true,
        GenerationGroupId = groupId
    };

    private static DetachedCopyMarker ValidateClone(string databaseEngine, IDbConnection connection)
    {
        DetachedCopyMarker marker = new DetachedCopyMarkerReader().Read(databaseEngine, connection);
        var target = new DatabaseTargetConfiguration
        {
            DatabaseEngine = databaseEngine,
            DatabaseName = connection.Database,
            DetachedCopyMarkerId = marker.MarkerId.ToString("D")
        };
        return new DetachedCopySafetyValidator(new DetachedCopyMarkerReader())
            .Validate(target, marker.MarkerId, connection);
    }

    private static void ExecuteNonQuery(IDbConnection connection, string sql)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static T ExecuteScalar<T>(IDbConnection connection, string sql)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? result = command.ExecuteScalar();
        if (result is null or DBNull)
        {
            throw new InvalidOperationException("The integration-test scalar query returned NULL.");
        }

        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string RequireConnectionString(string environmentVariable, string engine)
    {
        string? connectionString = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip($"Set {environmentVariable} to run the {engine} executor integration test.");
        }

        return connectionString;
    }
}
