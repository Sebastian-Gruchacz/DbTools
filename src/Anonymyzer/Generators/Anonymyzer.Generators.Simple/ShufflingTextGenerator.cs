namespace Anonymyzer.Generators.Simple;

using System.Linq.Expressions;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public class ShufflingTextGenerator : GeneratorBase<ShufflingTextGeneratorConfiguration>
{
    public override string Name { get; } = @"TextShuffler";
    public override DbDataType SupportedDataType { get; } = DbDataType.Text;

    public override bool IsMatch(IColumnInfo columnInfo)
    {
        // support only non-PK fields
        if (columnInfo.IsPartOfThePrimaryKey)
        {
            return false;
        }

        return true;
    }

    public override ShufflingTextGeneratorConfiguration GetDefaultConfiguration()
    {
        return new ShufflingTextGeneratorConfiguration
        {
            IterationsMultiplier = 1.1m,
            MinimumLengthToApply = 2 

            // TODO: any other config for scrambler?
        };
    }

    protected override Expression<Action<IRowNavigator>> BuildColumnWriter(IColumnInfo columnInfo, ShufflingTextGeneratorConfiguration config)
    {
        throw new NotImplementedException();
    }
}