namespace Anonymyzer.Console.Safety;

using System.Data;
using Anonymyzer.Configuration;

internal sealed class DetachedCopySafetyValidator(IDetachedCopyMarkerReader markerReader)
{
    public static void EnsureConfigurationDoesNotTargetMarker(AnonymizationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        TableProcessingOptions? markerTable = configuration.Tables.FirstOrDefault(table =>
            IsMarkerTable(configuration.Database.DatabaseEngine, table.SchemaName, table.TableName));
        if (markerTable is not null)
        {
            throw new InvalidOperationException(
                $"Configuration must not contain the safety marker table {markerTable.SchemaName}.{markerTable.TableName}.");
        }
    }

    public static bool IsMarkerTable(string databaseEngine, string schemaName, string tableName)
    {
        bool isSqlServer = databaseEngine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
        string expectedSchema = isSqlServer ? "dbo" : "public";
        string expectedTable = isSqlServer ? "__AnonymyzerDetachedCopy" : "__anonymyzer_detached_copy";
        return schemaName.Equals(expectedSchema, StringComparison.OrdinalIgnoreCase)
            && tableName.Equals(expectedTable, StringComparison.OrdinalIgnoreCase);
    }

    public DetachedCopyMarker Validate(
        DatabaseTargetConfiguration expectedTarget,
        Guid operatorConfirmedMarkerId,
        IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(expectedTarget);
        ArgumentNullException.ThrowIfNull(connection);

        if (operatorConfirmedMarkerId == Guid.Empty)
        {
            throw new InvalidOperationException("A non-empty marker id must be confirmed at runtime.");
        }

        if (!Guid.TryParse(expectedTarget.DetachedCopyMarkerId, out Guid configuredMarkerId)
            || configuredMarkerId == Guid.Empty)
        {
            throw new InvalidOperationException("The configuration has no valid detached-copy marker id.");
        }

        if (configuredMarkerId != operatorConfirmedMarkerId)
        {
            throw new InvalidOperationException("The runtime marker id does not match the configuration.");
        }

        if (!expectedTarget.DatabaseName.Equals(connection.Database, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Connected database '{connection.Database}' does not match configured database '{expectedTarget.DatabaseName}'.");
        }

        DetachedCopyMarker marker = markerReader.Read(expectedTarget.DatabaseEngine, connection);
        if (marker.MarkerId != configuredMarkerId)
        {
            throw new InvalidOperationException("The marker stored in the database does not match the configuration.");
        }

        if (!marker.DatabaseName.Equals(connection.Database, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Marker database '{marker.DatabaseName}' does not match connected database '{connection.Database}'.");
        }

        return marker;
    }
}
