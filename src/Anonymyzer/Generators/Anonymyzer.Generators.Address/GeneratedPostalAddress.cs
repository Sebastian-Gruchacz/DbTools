namespace Anonymyzer.Generators.Address;

public sealed record GeneratedPostalAddress(
    string Country,
    string Region,
    string City,
    string Street,
    string PostalCode);
