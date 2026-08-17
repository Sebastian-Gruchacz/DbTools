namespace Anonymyzer.Console.Safety;

using System.Data;

internal interface IDetachedCopyMarkerReader
{
    DetachedCopyMarker Read(string databaseEngine, IDbConnection connection);
}
