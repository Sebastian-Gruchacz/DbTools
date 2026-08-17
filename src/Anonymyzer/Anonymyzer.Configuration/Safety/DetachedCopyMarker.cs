namespace Anonymyzer.Configuration.Safety;

public sealed record DetachedCopyMarker(
    Guid MarkerId,
    string DatabaseName,
    DateTimeOffset CreatedUtc);
