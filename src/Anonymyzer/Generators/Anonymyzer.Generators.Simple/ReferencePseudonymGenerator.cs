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

        byte[] hmacKey = Encoding.UTF8.GetBytes(key);
        EncryptedExternalHashIndexBuilder? externalBuilder = null;
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var outputHashes = new HashSet<string>(StringComparer.Ordinal);
        long estimatedInMemoryBytes = 0;
        try
        {
            GeneratorDataRequirement lookup = GetDataRequirements(context.Binding, configuration)[1];
            using var hmac = new HMACSHA256(hmacKey);
            await foreach (GeneratorDataRow row in context.DataReader.ReadAsync(lookup, cancellationToken)
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                object? value = row.GetValue(configuration.LookupKeyColumn);
                if (value is null)
                {
                    throw new InvalidOperationException("The lookup primary key contains a null value.");
                }

                byte[] digest = ComputeHash(hmac, Canonicalize(value));
                try
                {
                    if (externalBuilder is not null)
                    {
                        externalBuilder.Add(digest);
                        continue;
                    }

                    string fullHash = Convert.ToHexString(digest);
                    if (hashes.Contains(fullHash))
                    {
                        continue;
                    }

                    string outputHash = fullHash[..configuration.HashLength];
                    long entryBytes = 160L + fullHash.Length * sizeof(char) + outputHash.Length * sizeof(char);
                    if (entryBytes > configuration.MaximumInMemoryBytes - estimatedInMemoryBytes)
                    {
                        if (configuration.OverflowStrategy == RelationalLookupOverflowStrategy.Fail)
                        {
                            throw new InvalidOperationException(
                                $"ReferencePseudonym exceeded its {configuration.MaximumInMemoryBytes:N0}-byte " +
                                "lookup memory limit. Increase MaximumInMemoryBytes or select EncryptedTemporaryIndex.");
                        }

                        externalBuilder = new EncryptedExternalHashIndexBuilder(
                            configuration.MaximumInMemoryBytes,
                            configuration.HashLength);
                        foreach (string bufferedHash in hashes)
                        {
                            byte[] bufferedDigest = Convert.FromHexString(bufferedHash);
                            try
                            {
                                externalBuilder.Add(bufferedDigest);
                            }
                            finally
                            {
                                CryptographicOperations.ZeroMemory(bufferedDigest);
                            }
                        }

                        hashes.Clear();
                        outputHashes.Clear();
                        externalBuilder.Add(digest);
                        continue;
                    }

                    if (!outputHashes.Add(outputHash))
                    {
                        throw new InvalidOperationException(
                            "ReferencePseudonym produced a collision. Increase HashLength and retry on a fresh clone.");
                    }

                    hashes.Add(fullHash);
                    estimatedInMemoryBytes += entryBytes;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(digest);
                }
            }

            IReferenceHashIndex index = externalBuilder?.Complete()
                ?? new InMemoryReferenceHashIndex(hashes);
            return new Session(
                context.Binding.GetRequiredOutput(ValueOutput),
                configuration.ReferenceColumn,
                configuration.Prefix,
                configuration.HashLength,
                configuration.PreserveNulls,
                hmacKey,
                index);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(hmacKey);
            throw;
        }
        finally
        {
            externalBuilder?.Dispose();
        }
    }

    private static byte[] ComputeHash(HMACSHA256 hmac, string canonicalKey)
    {
        byte[] canonicalBytes = Encoding.UTF8.GetBytes(canonicalKey);
        try
        {
            return hmac.ComputeHash(canonicalBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalBytes);
        }
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
        string prefix,
        int hashLength,
        bool preserveNulls,
        byte[] hmacKey,
        IReferenceHashIndex index) : IGeneratorSession
    {
        private readonly HMACSHA256 _hmac = new(hmacKey);

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? reference = row.GetValue(referenceColumn);
            if (reference is null && preserveNulls)
            {
                row.SetValue(outputColumn, null);
                return ValueTask.CompletedTask;
            }

            if (reference is null)
            {
                throw new InvalidOperationException(
                    "A target reference is absent from the configured lookup table. No key value was logged.");
            }

            byte[] digest = ComputeHash(_hmac, Canonicalize(reference));
            try
            {
                if (!index.Contains(digest))
                {
                    throw new InvalidOperationException(
                        "A target reference is absent from the configured lookup table. No key value was logged.");
                }

                row.SetValue(
                    outputColumn,
                    prefix + Convert.ToHexString(digest)[..hashLength].ToLowerInvariant());
                return ValueTask.CompletedTask;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _hmac.Dispose();
            CryptographicOperations.ZeroMemory(hmacKey);
            await index.DisposeAsync();
        }
    }
}
