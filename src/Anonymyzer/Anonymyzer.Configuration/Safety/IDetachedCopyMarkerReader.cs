namespace Anonymyzer.Configuration.Safety;

using System.Data;

public interface IDetachedCopyMarkerReader
{
    DetachedCopyMarker Read(string databaseEngine, IDbConnection connection);
}
