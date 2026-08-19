namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class ShufflingTextGenerator : GeneratorBase<ShufflingTextGeneratorConfiguration>
{
    public const string GeneratorType = "TextShuffler";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Text value shuffler",
        GeneratorExecutionScope.Column,
        DbDataType.Text)
    {
        Outputs = new[]
        {
            new GeneratorOutputDescriptor(ValueOutput, "Value", string.Empty, Required: true)
        }
    };

    private static readonly ShufflingTextGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        ShufflingTextGeneratorConfiguration configuration)
    {
        string columnName = binding.GetRequiredOutput(ValueOutput);
        return new[]
        {
            new GeneratorDataRequirement(
                "source-column",
                binding.Table,
                new[] { columnName },
                GeneratorValueSource.Original,
                RequiresCompleteScan: true)
        };
    }

    protected override async ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        ShufflingTextGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        GeneratorDataRequirement requirement = GetDataRequirements(context.Binding, configuration).Single();
        var values = new List<object?>();
        EncryptedExternalTextShuffleBuilder? externalBuilder = null;
        long estimatedInMemoryBytes = 0;
        long valueCount = 0;

        try
        {
            await foreach (GeneratorDataRow row in context.DataReader.ReadAsync(requirement, cancellationToken)
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                object? value = row.GetValue(columnName);
                if (configuration.PreserveNulls && value is null)
                {
                    continue;
                }

                long valueBytes = EncryptedExternalTextShuffleBuilder.EstimateInMemoryBytes(value);
                valueCount++;
                if (externalBuilder is not null)
                {
                    externalBuilder.Add(value);
                    continue;
                }

                if (estimatedInMemoryBytes + valueBytes <= configuration.MaximumInMemoryBytes)
                {
                    values.Add(value);
                    estimatedInMemoryBytes += valueBytes;
                    continue;
                }

                if (configuration.OverflowStrategy == ShuffleOverflowStrategy.Fail)
                {
                    throw new InvalidOperationException(
                        $"TextShuffler exceeded its {configuration.MaximumInMemoryBytes:N0}-byte memory limit. " +
                        "Increase MaximumInMemoryBytes or select EncryptedTemporaryFiles.");
                }

                externalBuilder = new EncryptedExternalTextShuffleBuilder(
                    configuration.Seed,
                    configuration.MaximumInMemoryBytes);
                foreach (object? bufferedValue in values)
                {
                    externalBuilder.Add(bufferedValue);
                }

                values.Clear();
                externalBuilder.Add(value);
            }

            if (externalBuilder is not null)
            {
                if (valueCount < configuration.MinimumPopulation)
                {
                    externalBuilder.Dispose();
                    return new ShufflingTextGeneratorSession(
                        columnName,
                        Array.Empty<object?>(),
                        configuration.PreserveNulls,
                        shouldApply: false);
                }

                IGeneratorSession session = externalBuilder.Complete(columnName, configuration.PreserveNulls);
                externalBuilder.Dispose();
                return session;
            }

            if (values.Count >= configuration.MinimumPopulation)
            {
                Shuffle(values, new Random(configuration.Seed));
            }

            return new ShufflingTextGeneratorSession(
                columnName,
                values,
                configuration.PreserveNulls,
                values.Count >= configuration.MinimumPopulation);
        }
        catch
        {
            externalBuilder?.Dispose();
            throw;
        }
    }

    private static void Shuffle(IList<object?> values, Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int replacementIndex = random.Next(index + 1);
            (values[index], values[replacementIndex]) = (values[replacementIndex], values[index]);
        }
    }

    private sealed class ShufflingTextGeneratorSession(
        string columnName,
        IReadOnlyList<object?> shuffledValues,
        bool preserveNulls,
        bool shouldApply) : IGeneratorSession
    {
        private int _index;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            object? currentValue = row.GetValue(columnName);
            if (!shouldApply || preserveNulls && currentValue is null)
            {
                return ValueTask.CompletedTask;
            }

            if (_index >= shuffledValues.Count)
            {
                throw new InvalidOperationException("The shuffler received more target rows than values prepared from the source column.");
            }

            row.SetValue(columnName, shuffledValues[_index]);
            _index++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
