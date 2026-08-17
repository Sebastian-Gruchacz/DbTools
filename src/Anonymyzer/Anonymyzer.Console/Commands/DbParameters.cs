namespace Anonymyzer.Console.Commands;

public class DbParameters
{
    /// <summary>
    /// Gets or sets name of used DatabaseEngine
    /// </summary>
    public string DatabaseEngine { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets name of processed DB (if not the part of <c>ConnectionString</c>)
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime-only connection string to the detached working copy.
    /// This value must never be persisted in the anonymyzation configuration.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime-only connection string required for structural operations.
    /// This value must never be persisted in the anonymyzation configuration.
    /// </summary>
    public string StructuralConnectionString { get; set; } = string.Empty;

}
