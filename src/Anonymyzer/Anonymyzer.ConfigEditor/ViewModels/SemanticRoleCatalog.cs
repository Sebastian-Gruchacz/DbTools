namespace Anonymyzer.ConfigEditor.ViewModels;

internal sealed record SemanticRoleOption(string Value, string DisplayName);

internal sealed record SemanticRoleGroup(string DisplayName, IReadOnlyList<SemanticRoleOption> Options);

internal static class SemanticRoleCatalog
{
    public static IReadOnlyList<SemanticRoleGroup> CreateDefault() =>
    [
        Group("None", Option(string.Empty, "No semantic role")),
        Group(
            "Person",
            Option("Person.FirstName", "First name"),
            Option("Person.LastName", "Last name"),
            Option("Person.FullName", "Full name"),
            Option("Person.BirthDate", "Birth date"),
            Option("Person.Gender", "Gender")),
        Group(
            "Contact",
            Option("Contact.Email", "Email"),
            Option("Contact.Phone", "Phone")),
        Group(
            "Address",
            Option("Address.Country", "Country"),
            Option("Address.Region", "Region"),
            Option("Address.City", "City"),
            Option("Address.Street", "Street"),
            Option("Address.PostalCode", "Postal code")),
        Group(
            "Identifiers",
            Option("Person.NationalId", "National / security number (PESEL, SSN)"),
            Option("Company.TaxId", "Tax / registry number (NIP, REGON, VAT)"),
            Option("Financial.BankAccount", "Bank account / IBAN")),
        Group("Organization", Option("Company.Name", "Company name")),
        Group("Account", Option("Account.Login", "Login"))
    ];

    private static SemanticRoleGroup Group(string displayName, params SemanticRoleOption[] options) =>
        new(displayName, options);

    private static SemanticRoleOption Option(string value, string displayName) => new(value, displayName);
}
