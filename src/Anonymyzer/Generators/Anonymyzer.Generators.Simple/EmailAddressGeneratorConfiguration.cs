namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class EmailAddressGeneratorConfiguration
{
    public EmailAddressPattern Pattern { get; set; } = EmailAddressPattern.Opaque;

    public string Domain { get; set; } = "example.invalid";

    public string OpaquePrefix { get; set; } = "person";

    public string FirstNameColumn { get; set; } = string.Empty;

    public string LastNameColumn { get; set; } = string.Empty;

    public GeneratorValueSource NameValueSource { get; set; } = GeneratorValueSource.Generated;

    public long StartAt { get; set; } = 1;

    public int MinimumDigits { get; set; } = 8;

    public bool PreserveNulls { get; set; } = true;
}
