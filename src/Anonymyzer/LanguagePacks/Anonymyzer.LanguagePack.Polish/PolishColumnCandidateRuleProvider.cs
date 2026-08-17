namespace Anonymyzer.LanguagePack.Polish;

using Anonymyzer.Base.Detection;

public sealed class PolishColumnCandidateRuleProvider : IColumnCandidateRuleProvider
{
    private const string Locale = "pl";

    private static readonly IReadOnlyList<ColumnCandidateRule> Rules = new[]
    {
        Rule("imie", "Person.FirstName", 0.98m),
        Rule("pierwsze_imie", "Person.FirstName", 0.98m),
        Rule("nazwisko", "Person.LastName", 0.99m),
        Rule("nazwisko_rodowe", "Person.LastName", 0.99m),
        Rule("pelne_imie", "Person.FullName", 0.96m),
        Rule("nazwa_kontaktu", "Person.FullName", 0.92m),
        Rule("data_urodzenia", "Person.BirthDate", 0.99m),
        Rule("plec", "Person.Gender", 0.96m),
        Rule("adres_email", "Contact.Email", 0.99m),
        Rule("telefon", "Contact.Phone", 0.98m),
        Rule("nr_telefonu", "Contact.Phone", 0.98m),
        Rule("numer_telefonu", "Contact.Phone", 0.99m),
        Rule("komorka", "Contact.Phone", 0.90m),
        Rule("kraj", "Address.Country", 0.92m),
        Rule("wojewodztwo", "Address.Region", 0.96m),
        Rule("powiat", "Address.Region", 0.88m),
        Rule("miasto", "Address.City", 0.98m),
        Rule("miejscowosc", "Address.City", 0.98m),
        Rule("ulica", "Address.Street", 0.98m),
        Rule("adres", "Address.Street", 0.86m),
        Rule("kod_pocztowy", "Address.PostalCode", 0.99m),
        Rule("nazwa_uzytkownika", "Account.Login", 0.98m),
        Rule("pesel", "Person.NationalId", 0.99m),
        Rule("numer_dowodu", "Person.NationalId", 0.98m),
        Rule("nr_dowodu", "Person.NationalId", 0.96m),
        Rule("nip", "Company.TaxId", 0.99m),
        Rule("regon", "Company.TaxId", 0.99m),
        Rule("numer_nip", "Company.TaxId", 0.99m),
        Rule("nazwa_firmy", "Company.Name", 0.96m),
        Rule("firma_nazwa", "Company.Name", 0.94m),
        Rule("rachunek_bankowy", "Financial.BankAccount", 0.98m),
        Rule("numer_konta", "Financial.BankAccount", 0.94m)
    };

    public IReadOnlyList<ColumnCandidateRule> GetRules() => Rules;

    private static ColumnCandidateRule Rule(string name, string role, decimal confidence) =>
        new($"pl:{name}", Locale, role, name, confidence);
}
