namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class GenderGenerator : GeneratorBase<GenderGeneratorConfiguration>
{
    public const string GeneratorType = "Gender";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Gender",
        GeneratorExecutionScope.Row,
        DbDataType.Text)
    {
        SupportsDeterministicReplay = true,
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Gender", "Person.Gender", Required: true)]
    };

    private static readonly GenderGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        GenderGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        GenderGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return ValueTask.FromResult<IGeneratorSession>(new Session(
            context.Binding.GetRequiredOutput(ValueOutput),
            configuration));
    }

    private sealed class Session(string columnName, GenderGeneratorConfiguration configuration) : IGeneratorSession
    {
        private readonly Random _random = new(configuration.Seed);

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                string value = _random.Next(100) < configuration.FemalePercentage
                    ? configuration.FemaleValue
                    : configuration.MaleValue;
                row.SetValue(columnName, value);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
