namespace Anonymyzer.Base.Generation;

public static class GeneratorColumnCompatibility
{
    public static bool Supports(GeneratorDescriptor descriptor, string? configuredDataType)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return string.IsNullOrWhiteSpace(configuredDataType)
               || descriptor.SupportedDataTypes.Any(dataType =>
                   configuredDataType.Equals(dataType.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public static string DescribeSupportedTypes(GeneratorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return string.Join(" or ", descriptor.SupportedDataTypes);
    }
}
