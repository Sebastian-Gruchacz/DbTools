namespace Anonymyzer.DatabaseAccess;

using System.Data;
using Anonymyzer.Base;
using Anonymyzer.Configuration;
using Anonymyzer.Configuration.Safety;
using Anonymyzer.PostgreSql;
using Anonymyzer.SqlServer;

internal sealed class ValidatedCloneConnectionFactory
{
    private readonly IReadOnlyList<IDbConnectionBuilder> _connectionBuilders = new IDbConnectionBuilder[]
    {
        new SqlServerConnectionBuilder(),
        new PostgreSqlConnectionBuilder()
    };

    private readonly DetachedCopySafetyValidator _safetyValidator =
        new(new DetachedCopyMarkerReader());

    public IDbConnection Open(
        AnonymizationConfiguration configuration,
        string connectionEnvironmentVariable,
        CancellationToken cancellationToken)
    {
        string? connectionString = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable '{connectionEnvironmentVariable}' is empty or missing.");
        }

        IDbConnectionBuilder builder = _connectionBuilders.SingleOrDefault(candidate =>
            candidate.Name.Equals(configuration.Database.DatabaseEngine, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Unsupported database engine '{configuration.Database.DatabaseEngine}'.");

        IDbConnection connection = builder.BuildMainConnection(
            connectionString,
            configuration.Database.DatabaseName);
        try
        {
            connection.Open();
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(configuration.Database.DetachedCopyMarkerId, out Guid markerId))
            {
                throw new InvalidOperationException("The configuration has no valid detached-copy marker id.");
            }

            _safetyValidator.Validate(configuration.Database, markerId, connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
