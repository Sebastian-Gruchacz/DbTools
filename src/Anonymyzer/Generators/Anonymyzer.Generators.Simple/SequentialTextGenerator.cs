namespace Anonymyzer.Generators.Simple;

using System.Globalization;
using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class SequentialTextGenerator : GeneratorBase<SequentialTextGeneratorConfiguration>
{
    public const string GeneratorType = "SequentialText";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Sequential text value",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Value", string.Empty, Required: true)]
    };

    private static readonly SequentialTextGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        SequentialTextGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        SequentialTextGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(columnName, configuration));
    }

    private sealed class Session(
        string columnName,
        SequentialTextGeneratorConfiguration configuration) : IGeneratorSession
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
                throw new InvalidOperationException("SequentialText exhausted the Int64 sequence range.");
            }

            string number = _next.ToString($"D{configuration.MinimumDigits}", CultureInfo.InvariantCulture);
            row.SetValue(columnName, $"{configuration.Prefix}{number}{configuration.Suffix}");
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
    }
}
