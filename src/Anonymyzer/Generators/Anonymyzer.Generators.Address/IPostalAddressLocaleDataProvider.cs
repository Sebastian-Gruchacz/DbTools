namespace Anonymyzer.Generators.Address;

public interface IPostalAddressLocaleDataProvider
{
    string Locale { get; }

    GeneratedPostalAddress Generate(Random random);
}
