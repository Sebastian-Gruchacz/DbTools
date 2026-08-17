namespace Anonymyzer.Console.Safety;

internal sealed record DetachedCopyMarker(
    Guid MarkerId,
    string DatabaseName,
    DateTimeOffset CreatedUtc);
