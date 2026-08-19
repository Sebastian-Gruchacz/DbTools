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
    public void PostExecutionValidatorFindsSqlServerCheckViolationWithoutReturningRowValues()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_SQLSERVER_CONNECTION", "SQL Server");
        string tableName = "__AnonymyzerValidation_" + Guid.NewGuid().ToString("N")[..12];
        string constraintName = "CK_" + tableName;
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE [dbo].[{tableName}] ([Id] int PRIMARY KEY, [Value] int NOT NULL); " +
            $"INSERT INTO [dbo].[{tableName}] ([Id], [Value]) VALUES (1, -7); " +
            $"ALTER TABLE [dbo].[{tableName}] WITH NOCHECK ADD CONSTRAINT [{constraintName}] CHECK ([Value] >= 0);");

        try
        {
            ConstraintValidationResult result = new PostExecutionDatabaseValidator().ValidateConstraints(
                connection,
                "SqlServer",
                new GeneratorTableReference("dbo", tableName));

            string issue = Assert.Single(result.Issues);
            Assert.Contains(constraintName, issue, StringComparison.Ordinal);
            Assert.DoesNotContain("-7", issue, StringComparison.Ordinal);
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE [dbo].[{tableName}];");
        }
    }

    [Fact]
    public void PostExecutionValidatorFindsPostgreSqlCheckViolationWithoutReturningRowValues()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_POSTGRES_CONNECTION", "PostgreSQL");
        string tableName = "__anonymyzer_validation_" + Guid.NewGuid().ToString("N")[..12];
        string constraintName = "ck_" + tableName;
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE public.\"{tableName}\" (\"Id\" integer PRIMARY KEY, \"Value\" integer NOT NULL); " +
            $"INSERT INTO public.\"{tableName}\" (\"Id\", \"Value\") VALUES (1, -7); " +
            $"ALTER TABLE public.\"{tableName}\" ADD CONSTRAINT \"{constraintName}\" " +
            "CHECK (\"Value\" >= 0) NOT VALID;");

        try
        {
            ConstraintValidationResult result = new PostExecutionDatabaseValidator().ValidateConstraints(
                connection,
                "PostgreSql",
                new GeneratorTableReference("public", tableName));

            string issue = Assert.Single(result.Issues);
            Assert.Contains(constraintName, issue, StringComparison.Ordinal);
            Assert.DoesNotContain("-7", issue, StringComparison.Ordinal);
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE public.\"{tableName}\";");
        }
    }

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

    [Fact]
    public async Task ExecutesReferencePseudonymAcrossPostgreSqlTablesWithoutChangingForeignKey()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_POSTGRES_CONNECTION", "PostgreSQL");
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string lookupTable = "__anonymyzer_lookup_" + suffix;
        string targetTable = "__anonymyzer_relational_" + suffix;
        string keyEnvironmentVariable = $"ANONYMYZER_TEST_PSEUDONYM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(keyEnvironmentVariable, "integration-key-with-more-than-thirty-two-characters");
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        DetachedCopyMarker marker = ValidateClone("PostgreSql", connection);
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE public.\"{lookupTable}\" (\"Id\" integer PRIMARY KEY); " +
            $"CREATE TABLE public.\"{targetTable}\" (\"Id\" integer PRIMARY KEY, " +
            $"\"DepartmentId\" integer REFERENCES public.\"{lookupTable}\"(\"Id\"), \"Alias\" varchar(100)); " +
            $"INSERT INTO public.\"{lookupTable}\" (\"Id\") VALUES (10), (20); " +
            $"INSERT INTO public.\"{targetTable}\" (\"Id\", \"DepartmentId\") VALUES (1, 10), (2, 20), (3, 10);");

        try
        {
            var generator = new ReferencePseudonymGenerator();
            AnonymizationConfiguration configuration = CreateReferencePseudonymConfiguration(
                generator,
                connection.Database,
                marker,
                targetTable,
                lookupTable,
                keyEnvironmentVariable);
            long processed = await ExecutePlanAsync(
                connection,
                new PostgreSqlAnonymyzerEngine(connection),
                "PostgreSql",
                configuration,
                [generator]);

            Assert.Equal(3, processed);
            Assert.Equal(3, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM public.\"{targetTable}\" WHERE \"DepartmentId\" IN (10, 20);"));
            Assert.Equal(1, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(DISTINCT \"Alias\") FROM public.\"{targetTable}\" WHERE \"DepartmentId\" = 10;"));
            Assert.Equal(2, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(DISTINCT \"Alias\") FROM public.\"{targetTable}\";"));
        }
        finally
        {
            ExecuteNonQuery(connection, $"DROP TABLE public.\"{targetTable}\"; DROP TABLE public.\"{lookupTable}\";");
            Environment.SetEnvironmentVariable(keyEnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task ExecutesJsonPathRedactorOnPostgreSqlJsonAndJsonbColumns()
    {
        string connectionString = RequireConnectionString("ANONYMYZER_POSTGRES_CONNECTION", "PostgreSQL");
        string tableName = "__anonymyzer_json_" + Guid.NewGuid().ToString("N")[..12];
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        DetachedCopyMarker marker = ValidateClone("PostgreSql", connection);
        ExecuteNonQuery(
            connection,
            $"CREATE TABLE public.\"{tableName}\" (\"Id\" integer PRIMARY KEY, \"JsonValue\" json, \"JsonbValue\" jsonb); " +
            $"INSERT INTO public.\"{tableName}\" (\"Id\", \"JsonValue\", \"JsonbValue\") VALUES " +
            "(1, '{\"secret\":\"Alice\",\"keep\":1}', '{\"secret\":\"Bob\",\"keep\":2}');");

        try
        {
            var generator = new JsonPathRedactorGenerator();
            AnonymizationConfiguration configuration = CreateJsonConfiguration(
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

            Assert.Equal(1, processed);
            Assert.Equal(1, ExecuteScalar<int>(
                connection,
                $"SELECT COUNT(*) FROM public.\"{tableName}\" WHERE " +
                "\"JsonValue\"->>'secret' = 'REDACTED' AND \"JsonbValue\"->>'secret' = 'REDACTED' " +
                "AND (\"JsonValue\"->>'keep')::integer = 1 AND (\"JsonbValue\"->>'keep')::integer = 2;"));
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

    private static AnonymizationConfiguration CreateReferencePseudonymConfiguration(
        ReferencePseudonymGenerator generator,
        string databaseName,
        DetachedCopyMarker marker,
        string targetTable,
        string lookupTable,
        string keyEnvironmentVariable)
    {
        const string profileId = "reference-pseudonym-fixture";
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
                    Options = generator.Configuration.Serialize(new ReferencePseudonymGeneratorConfiguration
                    {
                        ReferenceColumn = "DepartmentId",
                        LookupSchema = "public",
                        LookupTable = lookupTable,
                        LookupKeyColumn = "Id",
                        Prefix = "department-",
                        KeyEnvironmentVariable = keyEnvironmentVariable,
                        HashLength = 16
                    })
                }
            },
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = targetTable,
                    Enabled = true,
                    Columns =
                    {
                        new ColumnProcessingOptions
                        {
                            Ordinal = 3,
                            ColumnName = "Alias",
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

    private static AnonymizationConfiguration CreateJsonConfiguration(
        JsonPathRedactorGenerator generator,
        string databaseName,
        DetachedCopyMarker marker,
        string tableName)
    {
        const string profileId = "json-fixture";
        var configuration = new AnonymizationConfiguration
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
                    Options = generator.Configuration.Serialize(new JsonPathRedactorGeneratorConfiguration
                    {
                        Rules =
                        [
                            new JsonPathRedactionRuleConfiguration
                            {
                                Path = "$/secret",
                                ReplacementJson = "\"REDACTED\""
                            }
                        ],
                        RequireEveryPath = true
                    })
                }
            }
        };
        var table = new TableProcessingOptions
        {
            SchemaName = "public",
            TableName = tableName,
            Enabled = true
        };
        table.Columns.Add(CreateJsonColumn("JsonValue", ordinal: 2, generator, profileId));
        table.Columns.Add(CreateJsonColumn("JsonbValue", ordinal: 3, generator, profileId));
        configuration.Tables.Add(table);
        return configuration;
    }

    private static ColumnProcessingOptions CreateJsonColumn(
        string columnName,
        int ordinal,
        JsonPathRedactorGenerator generator,
        string profileId) => new()
    {
        Ordinal = ordinal,
        ColumnName = columnName,
        DataType = DbDataType.Json.ToString(),
        MaxLength = 0,
        Unicode = true,
        Enabled = true,
        Generator = new ColumnGeneratorConfiguration
        {
            GeneratorType = generator.Descriptor.Type,
            GeneratorVersion = generator.Descriptor.Version,
            ProfileId = profileId
        }
    };

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
