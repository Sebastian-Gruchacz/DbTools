namespace Anonymyzer.Base.Generation;

public sealed record GeneratorOutputDescriptor(
    string Name,
    string DisplayName,
    string SemanticRole,
    bool Required);
