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
    /// Gets or sets connection string to the operational DB
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets connection string required for master / structural operations
    /// </summary>
    public string StructuralConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Need to do this, as stupid JSON writers all properties of derived class assigned to the base variable...
    /// </summary>
    /// <returns></returns>
    public DbParameters GetCleanParameters()
    {
        return new DbParameters
        {
            DatabaseEngine = this.DatabaseEngine,
            StructuralConnectionString = this.StructuralConnectionString,
            ConnectionString = this.ConnectionString,
            DatabaseName = this.DatabaseName
            
            
        };
    }
}