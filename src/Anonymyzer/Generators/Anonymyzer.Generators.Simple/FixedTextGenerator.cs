namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class FixedTextGenerator : GeneratorBase<FixedTextGeneratorConfiguration>
{
    public const string GeneratorType = "FixedText";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Fixed text value",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Value", string.Empty, Required: true)]
    };

    private static readonly FixedTextGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        FixedTextGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        FixedTextGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        string columnName = context.Binding.GetRequiredOutput(ValueOutput);
        return ValueTask.FromResult<IGeneratorSession>(new Session(
            columnName,
            configuration.Value,
            configuration.PreserveNulls));
    }

    private sealed class Session(string columnName, string value, bool preserveNulls) : IGeneratorSession
    {
        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!preserveNulls || row.GetValue(columnName) is not null)
            {
                row.SetValue(columnName, value);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
