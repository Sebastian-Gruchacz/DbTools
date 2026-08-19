namespace Anonymyzer.Base.Generation;

public sealed record WeightedValue<T>(T Value, long Weight);

public sealed class WeightedRandomTable<T>
{
    private readonly Entry[] _entries;
    private readonly long _totalWeight;

    public WeightedRandomTable(IEnumerable<WeightedValue<T>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var entries = new List<Entry>();
        long cumulativeWeight = 0;
        foreach (WeightedValue<T> item in values)
        {
            if (item.Weight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Every weight must be greater than zero.");
            }

            cumulativeWeight = checked(cumulativeWeight + item.Weight);
            entries.Add(new Entry(item.Value, cumulativeWeight));
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("At least one weighted value is required.", nameof(values));
        }

        _entries = entries.ToArray();
        _totalWeight = cumulativeWeight;
    }

    public T Select(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        long value = random.NextInt64(_totalWeight);
        int lower = 0;
        int upper = _entries.Length - 1;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (value < _entries[middle].CumulativeWeight)
            {
                upper = middle;
            }
            else
            {
                lower = middle + 1;
            }
        }

        return _entries[lower].Value;
    }

    private sealed record Entry(T Value, long CumulativeWeight);
}
