namespace Anonymyzer.LanguagePack.English;

using Anonymyzer.Base.Detection;

public sealed class EnglishColumnCandidateRuleProvider : IColumnCandidateRuleProvider
{
    private const string Locale = "en";

    private static readonly IReadOnlyList<ColumnCandidateRule> Rules = new[]
    {
        Rule("first_name", "Person.FirstName", 0.98m),
        Rule("firstname", "Person.FirstName", 0.96m),
        Rule("given_name", "Person.FirstName", 0.96m),
        Rule("forename", "Person.FirstName", 0.94m),
        Rule("preferred_name", "Person.FirstName", 0.90m),
        Rule("last_name", "Person.LastName", 0.98m),
        Rule("lastname", "Person.LastName", 0.96m),
        Rule("surname", "Person.LastName", 0.98m),
        Rule("family_name", "Person.LastName", 0.96m),
        Rule("full_name", "Person.FullName", 0.96m),
        Rule("display_name", "Person.FullName", 0.86m),
        Rule("contact_name", "Person.FullName", 0.92m),
        Rule("birth_date", "Person.BirthDate", 0.98m),
        Rule("date_of_birth", "Person.BirthDate", 0.98m),
        Rule("dob", "Person.BirthDate", 0.90m),
        Rule("gender", "Person.Gender", 0.96m),
        Rule("email", "Contact.Email", 0.99m),
        Rule("email_address", "Contact.Email", 0.99m),
        Rule("e_mail", "Contact.Email", 0.99m),
        Rule("phone", "Contact.Phone", 0.96m),
        Rule("phone_number", "Contact.Phone", 0.98m),
        Rule("mobile", "Contact.Phone", 0.91m),
        Rule("telephone", "Contact.Phone", 0.96m),
        Rule("country", "Address.Country", 0.92m),
        Rule("state", "Address.Region", 0.82m),
        Rule("province", "Address.Region", 0.90m),
        Rule("city", "Address.City", 0.94m),
        Rule("town", "Address.City", 0.88m),
        Rule("street", "Address.Street", 0.92m),
        Rule("street_name", "Address.Street", 0.96m),
        Rule("street_address", "Address.Street", 0.96m),
        Rule("mailing_address", "Address.Street", 0.92m),
        Rule("postal_address", "Address.Street", 0.94m),
        Rule("address_line_1", "Address.Street", 0.94m),
        Rule("address_line1", "Address.Street", 0.94m),
        Rule("address1", "Address.Street", 0.90m),
        Rule("address_line_2", "Address.Street", 0.92m),
        Rule("address_line2", "Address.Street", 0.92m),
        Rule("address2", "Address.Street", 0.88m),
        Rule("address", "Address.Street", 0.86m, "ip", "mac", "memory", "net", "network", "web", "website"),
        Rule("postal_code", "Address.PostalCode", 0.98m),
        Rule("postcode", "Address.PostalCode", 0.96m),
        Rule("zip_code", "Address.PostalCode", 0.98m),
        Rule("zipcode", "Address.PostalCode", 0.96m),
        Rule("login", "Account.Login", 0.92m),
        Rule("username", "Account.Login", 0.96m),
        Rule("user_name", "Account.Login", 0.96m),
        Rule("logon_name", "Account.Login", 0.96m),
        Rule("screen_name", "Account.Login", 0.92m),
        Rule("ssn", "Person.NationalId", 0.99m),
        Rule("national_id", "Person.NationalId", 0.98m),
        Rule("identity_number", "Person.NationalId", 0.96m),
        Rule("tax_id", "Company.TaxId", 0.98m),
        Rule("vat_number", "Company.TaxId", 0.96m),
        Rule("company_name", "Company.Name", 0.94m),
        Rule("legal_name", "Company.Name", 0.90m),
        Rule("business_name", "Company.Name", 0.92m),
        Rule("supplier_name", "Company.Name", 0.94m),
        Rule("iban", "Financial.BankAccount", 0.99m),
        Rule("bank_account", "Financial.BankAccount", 0.96m, "branch", "code", "name")
    };

    public IReadOnlyList<ColumnCandidateRule> GetRules() => Rules;

    private static ColumnCandidateRule Rule(
        string name,
        string role,
        decimal confidence,
        params string[] excludedTokens) =>
        new ColumnCandidateRule(
            $"en:{name}",
            Locale,
            role,
            name,
            confidence)
        {
            ExcludedTokens = new HashSet<string>(excludedTokens, StringComparer.Ordinal)
        };
}
