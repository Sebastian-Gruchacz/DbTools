namespace Anonymyzer.Configuration.Safety;

using System.Data;
using System.Globalization;

public sealed class DetachedCopyMarkerReader : IDetachedCopyMarkerReader
{
    public DetachedCopyMarker Read(string databaseEngine, IDbConnection connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseEngine);
        ArgumentNullException.ThrowIfNull(connection);

        using IDbCommand command = connection.CreateCommand();
        command.CommandText = databaseEngine.ToUpperInvariant() switch
        {
            "SQLSERVER" => "SELECT MarkerId, DatabaseName, CreatedUtc FROM dbo.__AnonymyzerDetachedCopy;",
            "POSTGRESQL" => "SELECT marker_id, database_name, created_utc FROM public.__anonymyzer_detached_copy;",
            _ => throw new InvalidOperationException($"Unsupported database engine '{databaseEngine}'.")
        };

        using IDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("The detached-copy marker table is empty.");
        }

        var marker = new DetachedCopyMarker(
            ParseGuid(reader.GetValue(0)),
            Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
            ParseTimestamp(reader.GetValue(2)));

        if (reader.Read())
        {
            throw new InvalidOperationException("The detached-copy marker table must contain exactly one row.");
        }

        return marker;
    }

    private static Guid ParseGuid(object value)
    {
        return value is Guid guid
            ? guid
            : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static DateTimeOffset ParseTimestamp(object value)
    {
        return value switch
        {
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(timestamp),
            _ => DateTimeOffset.Parse(
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                CultureInfo.InvariantCulture)
        };
    }
}
