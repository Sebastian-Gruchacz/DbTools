namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Generators.Address;

public sealed class PolishPostalAddressLocaleDataProvider : IPostalAddressLocaleDataProvider
{
    private static readonly Location[] Locations =
    {
        new("Mazowieckie", "Warszawa", "00-001", ["Marszałkowska", "Puławska", "Targowa"]),
        new("Małopolskie", "Kraków", "30-001", ["Długa", "Karmelicka", "Kalwaryjska"]),
        new("Dolnośląskie", "Wrocław", "50-001", ["Legnicka", "Piłsudskiego", "Grabiszyńska"]),
        new("Wielkopolskie", "Poznań", "60-001", ["Głogowska", "Dąbrowskiego", "Święty Marcin"]),
        new("Pomorskie", "Gdańsk", "80-001", ["Długa", "Grunwaldzka", "Kartuska"]),
        new("Łódzkie", "Łódź", "90-001", ["Piotrkowska", "Zgierska", "Narutowicza"])
    };

    public string Locale => "pl-PL";

    public GeneratedPostalAddress Generate(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        Location location = Locations[random.Next(Locations.Length)];
        string street = location.Streets[random.Next(location.Streets.Length)];
        return new GeneratedPostalAddress(
            "Polska",
            location.Region,
            location.City,
            $"ul. {street} {random.Next(1, 200)}",
            location.PostalCode);
    }

    private sealed record Location(string Region, string City, string PostalCode, string[] Streets);
}
