namespace Anonymyzer.Console.Configuration;

using Anonymyzer.Base;

internal class ColumnProcessingOptions
{
    public bool Enabled { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public int MaxLength { get; set; }
    public bool Unicode { get; set; }
    public string DataType { get; set; }

    public ColumnGeneratorConfiguration Generator { get; set; }
}