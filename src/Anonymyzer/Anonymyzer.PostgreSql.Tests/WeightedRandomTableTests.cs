namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;

public sealed class WeightedRandomTableTests
{
    [Fact]
    public void SelectsDeterministicallyAccordingToRelativeWeights()
    {
        var table = new WeightedRandomTable<string>(
        [
            new("rare", 1),
            new("common", 3)
        ]);

        string[] first = Select(table, seed: 1234, count: 10_000);
        string[] second = Select(table, seed: 1234, count: 10_000);

        Assert.Equal(first, second);
        Assert.InRange(first.Count(value => value == "common"), 7_200, 7_800);
    }

    [Fact]
    public void RejectsEmptyOrNonPositiveWeights()
    {
        Assert.Throws<ArgumentException>(() => new WeightedRandomTable<string>([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightedRandomTable<string>([new("invalid", 0)]));
    }

    private static string[] Select(WeightedRandomTable<string> table, int seed, int count)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count).Select(_ => table.Select(random)).ToArray();
    }
}
