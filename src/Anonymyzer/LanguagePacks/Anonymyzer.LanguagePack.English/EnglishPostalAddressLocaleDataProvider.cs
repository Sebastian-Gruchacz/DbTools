namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Generators.Address;

public sealed class EnglishPostalAddressLocaleDataProvider : IPostalAddressLocaleDataProvider
{
    private static readonly Location[] Locations =
    {
        new("New York", "New York", "10001", ["Broadway", "8th Avenue", "West 31st Street"]),
        new("California", "Los Angeles", "90001", ["Main Street", "Central Avenue", "Florence Avenue"]),
        new("Illinois", "Chicago", "60601", ["Michigan Avenue", "Lake Street", "Wabash Avenue"]),
        new("Texas", "Houston", "77002", ["Main Street", "Travis Street", "Louisiana Street"]),
        new("Arizona", "Phoenix", "85003", ["Central Avenue", "Washington Street", "Jefferson Street"]),
        new("Pennsylvania", "Philadelphia", "19103", ["Market Street", "Chestnut Street", "Walnut Street"])
    };

    public string Locale => "en-US";

    public GeneratedPostalAddress Generate(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        Location location = Locations[random.Next(Locations.Length)];
        string street = location.Streets[random.Next(location.Streets.Length)];
        return new GeneratedPostalAddress(
            "United States",
            location.Region,
            location.City,
            $"{random.Next(1, 10_000)} {street}",
            location.PostalCode);
    }

    private sealed record Location(string Region, string City, string PostalCode, string[] Streets);
}
