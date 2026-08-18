namespace Anonymyzer.Generators.Person;

using Anonymyzer.Base;
using Anonymyzer.Base.Generation;

public sealed class BirthDateGenerator : GeneratorBase<BirthDateGeneratorConfiguration>
{
    public const string GeneratorType = "BirthDate";
    public const string GeneratorVersion = "1.0.0";
    public const string ValueOutput = "Value";

    private static readonly GeneratorDescriptor GeneratorDescriptor = new(
        GeneratorType,
        GeneratorVersion,
        "Birth date",
        GeneratorExecutionScope.Row,
        DbDataType.Date)
    {
        Outputs = [new GeneratorOutputDescriptor(ValueOutput, "Birth date", "Person.BirthDate", Required: true)],
        SupportedDataTypes = [DbDataType.Date, DbDataType.DateTime]
    };

    private static readonly BirthDateGeneratorConfigurationCodec ConfigurationCodec = new();

    public override GeneratorDescriptor Descriptor => GeneratorDescriptor;

    public override IGeneratorConfigurationCodec Configuration => ConfigurationCodec;

    protected override IReadOnlyList<GeneratorDataRequirement> GetDataRequirements(
        GeneratorBinding binding,
        BirthDateGeneratorConfiguration configuration)
    {
        binding.GetRequiredOutput(ValueOutput);
        return Array.Empty<GeneratorDataRequirement>();
    }

    protected override ValueTask<IGeneratorSession> PrepareAsync(
        GeneratorPreparationContext context,
        BirthDateGeneratorConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> errors = Configuration.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        BirthDateGeneratorConfigurationCodec.TryParseDate(configuration.MinimumDate, out DateOnly minimum);
        BirthDateGeneratorConfigurationCodec.TryParseDate(configuration.MaximumDate, out DateOnly maximum);
        return ValueTask.FromResult<IGeneratorSession>(new Session(
            context.Binding.GetRequiredOutput(ValueOutput),
            minimum,
            maximum,
            configuration));
    }

    private sealed class Session(
        string columnName,
        DateOnly minimum,
        DateOnly maximum,
        BirthDateGeneratorConfiguration configuration) : IGeneratorSession
    {
        private readonly Random _random = new(configuration.Seed);
        private readonly int _dayCount = maximum.DayNumber - minimum.DayNumber + 1;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuration.PreserveNulls || row.GetValue(columnName) is not null)
            {
                row.SetValue(columnName, minimum.AddDays(_random.Next(_dayCount)));
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
