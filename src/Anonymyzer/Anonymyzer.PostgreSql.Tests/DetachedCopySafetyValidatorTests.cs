namespace Anonymyzer.PostgreSql.Tests;

using System.Data;
using System.Diagnostics.CodeAnalysis;
using Anonymyzer.Configuration;
using Anonymyzer.Console.Safety;

public sealed class DetachedCopySafetyValidatorTests
{
    private static readonly Guid MarkerId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void AcceptsOnlyMatchingRuntimeConfigurationConnectionAndDatabaseMarker()
    {
        var marker = new DetachedCopyMarker(MarkerId, "detached_clone", DateTimeOffset.UtcNow);
        var validator = new DetachedCopySafetyValidator(new StubMarkerReader(marker));
        var target = CreateTarget();

        DetachedCopyMarker result = validator.Validate(target, MarkerId, new StubConnection("detached_clone"));

        Assert.Same(marker, result);
    }

    [Fact]
    public void RejectsRuntimeMarkerDifferentFromConfigurationBeforeReadingDatabase()
    {
        var reader = new StubMarkerReader(
            new DetachedCopyMarker(MarkerId, "detached_clone", DateTimeOffset.UtcNow));
        var validator = new DetachedCopySafetyValidator(reader);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(CreateTarget(), Guid.NewGuid(), new StubConnection("detached_clone")));

        Assert.Contains("runtime marker", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(reader.WasCalled);
    }

    [Fact]
    public void RejectsConnectedDatabaseWithDifferentName()
    {
        var marker = new DetachedCopyMarker(MarkerId, "detached_clone", DateTimeOffset.UtcNow);
        var validator = new DetachedCopySafetyValidator(new StubMarkerReader(marker));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(CreateTarget(), MarkerId, new StubConnection("production")));

        Assert.Contains("does not match configured database", exception.Message);
    }

    [Fact]
    public void RejectsConfigurationThatTargetsMarkerTable()
    {
        var configuration = new AnonymizationConfiguration
        {
            Database = CreateTarget(),
            Tables =
            {
                new TableProcessingOptions
                {
                    SchemaName = "public",
                    TableName = "__anonymyzer_detached_copy"
                }
            }
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            DetachedCopySafetyValidator.EnsureConfigurationDoesNotTargetMarker(configuration));

        Assert.Contains("safety marker table", exception.Message);
    }

    private static DatabaseTargetConfiguration CreateTarget()
    {
        return new DatabaseTargetConfiguration
        {
            DatabaseEngine = "PostgreSql",
            DatabaseName = "detached_clone",
            DetachedCopyMarkerId = MarkerId.ToString("D")
        };
    }

    private sealed class StubMarkerReader(DetachedCopyMarker marker) : IDetachedCopyMarkerReader
    {
        public bool WasCalled { get; private set; }

        public DetachedCopyMarker Read(string databaseEngine, IDbConnection connection)
        {
            WasCalled = true;
            return marker;
        }
    }

    private sealed class StubConnection(string database) : IDbConnection
    {
        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => database;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
