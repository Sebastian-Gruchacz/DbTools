namespace Anonymyzer.Generators.Simple;

public sealed class ReferencePseudonymGeneratorConfiguration
{
    public string ReferenceColumn { get; set; } = string.Empty;

    public string LookupSchema { get; set; } = string.Empty;

    public string LookupTable { get; set; } = string.Empty;

    public string LookupKeyColumn { get; set; } = string.Empty;

    public string Prefix { get; set; } = "anon-";

    public string KeyEnvironmentVariable { get; set; } = "ANONYMYZER_PSEUDONYM_KEY";

    public int HashLength { get; set; } = 24;

    public long MaximumInMemoryBytes { get; set; } = 64L * 1024 * 1024;

    public RelationalLookupOverflowStrategy OverflowStrategy { get; set; } = RelationalLookupOverflowStrategy.Fail;

    public bool PreserveNulls { get; set; } = true;
}

public enum RelationalLookupOverflowStrategy
{
    Fail,
    EncryptedTemporaryIndex
}
