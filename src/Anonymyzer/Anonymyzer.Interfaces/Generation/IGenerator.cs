namespace Anonymyzer.Base.Generation;

using System.Linq.Expressions;

using Anonymyzer.Base;

using Newtonsoft.Json.Linq;

public interface IGenerator
{
    /// <summary>
    /// Unique name of the generator
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Data type of the column, that this generator supports
    /// </summary>
    DbDataType SupportedDataType { get; }

    /// <summary>
    /// Returns TRUE if this generator is capable of processing provided column (more checks, than just data-type)
    /// </summary>
    /// <param name="columnInfo"></param>
    /// <returns></returns>
    bool IsMatch(IColumnInfo columnInfo);

    /// <summary>
    /// Retrieves default configuration for this generator
    /// </summary>
    /// <returns></returns>
    JObject GetDefaultConfig();

    /// <summary>
    /// Retrieves processing function to operate on a given column having both global and particular configurations
    /// </summary>
    /// <param name="columnInfo"></param>
    /// <param name="globalConfig"></param>
    /// <param name="columnConfig"></param>
    /// <returns></returns>
    Expression<Action<IRowNavigator>> BuildColumnWriter(IColumnInfo columnInfo, JObject globalConfig, JObject columnConfig);
}