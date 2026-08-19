namespace Anonymyzer.Generators.Simple;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class ReferencePseudonymGenerator : GeneratorBase<ReferencePseudonymGeneratorConfiguration>
{
    public const string GeneratorType = "ReferencePseudonym";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Consistent pseudonym by reference",
        GeneratorExecutionScope.Relational,
        DbDataType.Text)
    {
        Outputs =
        [
            new GeneratorOutputDescriptor(ValueOutput, "Value", string.Empty, Required: true)
        ]
    };

    private static readonly ReferencePseudonymGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        ReferencePseudonymGeneratorConfiguration configuration)
    {
        if (binding.GetRequiredOutput(ValueOutput).Equals(
                configuration.ReferenceColumn,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ReferencePseudonym cannot overwrite its reference column.");
        }

        return
        [
            new GeneratorDataRequirement(
                "target-reference",
                binding.Table,
                [configuration.ReferenceColumn],
                GeneratorValueSource.Original,
                RequiresCompleteScan: false),
            new GeneratorDataRequirement(
                "lookup-keys",
                new GeneratorTableReference(configuration.LookupSchema, configuration.LookupTable),
                [configuration.LookupKeyColumn],
                GeneratorValueSource.Original,
                RequiresCompleteScan: true)
        ];
    }

    protected override async ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        ReferencePseudonymGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string key = Environment.GetEnvironmentVariable(configuration.KeyEnvironmentVariable) ?? string.Empty;
        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                $"Environment variable '{configuration.KeyEnvironmentVariable}' must contain at least 32 characters.");
        }

        GeneratorDataRequirement lookup = GetDataRequirements(context.Binding, configuration)[1];
        var pseudonyms = new Dictionary<string, string>(StringComparer.Ordinal);
        var generatedValues = new HashSet<string>(StringComparer.Ordinal);
        long estimatedInMemoryBytes = 0;
        await foreach (GeneratorDataRow row in context.DataReader.ReadAsync(lookup, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            object? value = row.GetValue(configuration.LookupKeyColumn);
            if (value is null)
            {
                throw new InvalidOperationException("The lookup primary key contains a null value.");
            }

            string canonicalKey = Canonicalize(value);
            if (pseudonyms.ContainsKey(canonicalKey))
            {
                continue;
            }

            string pseudonym = CreatePseudonym(canonicalKey, configuration, key);
            long entryBytes = 128L
                              + Encoding.UTF8.GetByteCount(canonicalKey)
                              + Encoding.UTF8.GetByteCount(pseudonym);
            if (entryBytes > configuration.MaximumInMemoryBytes - estimatedInMemoryBytes)
            {
                throw new InvalidOperationException(
                    $"ReferencePseudonym exceeded its {configuration.MaximumInMemoryBytes:N0}-byte lookup memory limit.");
            }

            if (!generatedValues.Add(pseudonym))
            {
                throw new InvalidOperationException(
                    "ReferencePseudonym produced a collision. Increase HashLength and retry on a fresh clone.");
            }

            pseudonyms.Add(canonicalKey, pseudonym);
            estimatedInMemoryBytes += entryBytes;
        }

        return new Session(
            context.Binding.GetRequiredOutput(ValueOutput),
            configuration.ReferenceColumn,
            configuration.PreserveNulls,
            pseudonyms);
    }

    private static string CreatePseudonym(
        string canonicalKey,
        ReferencePseudonymGeneratorConfiguration configuration,
        string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        byte[] digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalKey));
        return configuration.Prefix + Convert.ToHexString(digest)[..configuration.HashLength].ToLowerInvariant();
    }

    private static string Canonicalize(object value) => value switch
    {
        byte[] bytes => Convert.ToHexString(bytes),
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? throw new InvalidOperationException("A lookup key could not be canonicalized.")
    };

    private sealed class Session(
        string outputColumn,
        string referenceColumn,
        bool preserveNulls,
        IReadOnlyDictionary<string, string> pseudonyms) : IGeneratorSession
    {
        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? reference = row.GetValue(referenceColumn);
            if (reference is null && preserveNulls)
            {
                row.SetValue(outputColumn, null);
                return ValueTask.CompletedTask;
            }

            if (reference is null || !pseudonyms.TryGetValue(Canonicalize(reference), out string? pseudonym))
            {
                throw new InvalidOperationException(
                    "A target reference is absent from the configured lookup table. No key value was logged.");
            }

            row.SetValue(outputColumn, pseudonym);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
