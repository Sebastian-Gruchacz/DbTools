namespace Anonymyzer.Configuration;

public sealed class ColumnProcessingOptions
{
    public int Ordinal { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public int MaxLength { get; set; }

    public bool Unicode { get; set; }

    public string SchemaStatus { get; set; } = "Current";

    public bool Enabled { get; set; }

    public string SemanticRole { get; set; } = string.Empty;

    public string GenerationGroupId { get; set; } = string.Empty;

    public CandidateDetectionConfiguration Detection { get; set; } = new();

    public ColumnOperatorOverrides OperatorOverrides { get; set; } = new();

    public ColumnGeneratorConfiguration Generator { get; set; } = new();
}
