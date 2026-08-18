namespace Anonymyzer.Generators.Simple;

using Anonymyzer.Base.Generation;

public sealed class AccountLoginGeneratorConfiguration
{
    public AccountLoginPattern Pattern { get; set; } = AccountLoginPattern.Opaque;
    public string OpaquePrefix { get; set; } = "user";
    public string FirstNameColumn { get; set; } = string.Empty;
    public string LastNameColumn { get; set; } = string.Empty;
    public GeneratorValueSource NameValueSource { get; set; } = GeneratorValueSource.Generated;
    public string Separator { get; set; } = ".";
    public long StartAt { get; set; } = 1;
    public int MinimumDigits { get; set; } = 6;
    public bool PreserveNulls { get; set; } = true;
}
