namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base.Generation;
using Anonymyzer.Generators.Person;

public sealed class GeneratorColumnCompatibilityTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("date", true)]
    [InlineData("DATETIME", true)]
    [InlineData("Text", false)]
    public void MatchesConfiguredTypeAgainstEveryTypeSupportedByGenerator(
        string? configuredDataType,
        bool expected)
    {
        GeneratorDescriptor descriptor = new BirthDateGenerator().Descriptor;

        bool result = GeneratorColumnCompatibility.Supports(descriptor, configuredDataType);

        Assert.Equal(expected, result);
        Assert.Equal("Date or DateTime", GeneratorColumnCompatibility.DescribeSupportedTypes(descriptor));
    }
}
