namespace Anonymyzer.Configuration;

/// <summary>
/// Identifies choices made explicitly by the operator. A database rescan must not overwrite them.
/// </summary>
public sealed class ColumnOperatorOverrides
{
    public bool Enabled { get; set; }

    public bool SemanticRole { get; set; }

    public bool Generator { get; set; }

    public bool GenerationGroup { get; set; }

    public bool HasAny => Enabled || SemanticRole || Generator || GenerationGroup;
}
