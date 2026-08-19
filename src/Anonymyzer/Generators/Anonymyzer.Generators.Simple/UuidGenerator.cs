namespace Anonymyzer.Generators.Simple;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class UuidGenerator : GeneratorBase<UuidGeneratorConfiguration>
{
    public const string GeneratorType = "Uuid";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "UUID text",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "UUID", string.Empty, Required: true)]
    };

    private static readonly UuidGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        UuidGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        UuidGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        byte[] seedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuration.Seed));
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, seedHash, configuration));
    }

    private sealed class Session(
        string columnName,
        byte[] seedHash,
        UuidGeneratorConfiguration configuration) : IGeneratorSession
    {
        private long _next = configuration.StartAt;
        private bool _exhausted;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configuration.PreserveNulls && row.GetValue(columnName) is null)
            {
                return ValueTask.CompletedTask;
            }

            if (_exhausted)
            {
                throw new InvalidOperationException("Uuid exhausted the Int64 sequence range.");
            }

            row.SetValue(columnName, CreateValue(_next));
            if (_next == long.MaxValue)
            {
                _exhausted = true;
            }
            else
            {
                _next++;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private string CreateValue(long ordinal)
        {
            Span<byte> input = stackalloc byte[40];
            seedHash.CopyTo(input);
            BinaryPrimitives.WriteInt64BigEndian(input[32..], ordinal);
            byte[] uuidBytes = SHA256.HashData(input)[..16];
            uuidBytes[7] = (byte)((uuidBytes[7] & 0x0f) | 0x40);
            uuidBytes[8] = (byte)((uuidBytes[8] & 0x3f) | 0x80);

            string format = configuration.Format switch
            {
                UuidTextFormat.Hyphenated => "D",
                UuidTextFormat.Compact => "N",
                UuidTextFormat.Braced => "B",
                UuidTextFormat.Parenthesized => "P",
                _ => throw new InvalidOperationException($"Unsupported UUID format '{configuration.Format}'.")
            };
            string value = new Guid(uuidBytes).ToString(format, CultureInfo.InvariantCulture);
            return configuration.Uppercase ? value.ToUpperInvariant() : value;
        }
    }
}
