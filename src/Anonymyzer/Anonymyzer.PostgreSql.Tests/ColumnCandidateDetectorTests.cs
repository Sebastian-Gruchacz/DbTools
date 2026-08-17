namespace Anonymyzer.PostgreSql.Tests;

using Anonymyzer.Base;
using Anonymyzer.Base.Detection;
using Anonymyzer.Configuration;
using Anonymyzer.Console.GenerateConfiguration;
using Anonymyzer.LanguagePack.English;
using Anonymyzer.LanguagePack.Polish;

public sealed class ColumnCandidateDetectorTests
{
    private readonly ColumnCandidateDetector _detector = new(
        new IColumnCandidateRuleProvider[]
        {
            new EnglishColumnCandidateRuleProvider(),
            new PolishColumnCandidateRuleProvider()
        });

    [Theory]
    [InlineData("CustomerFirstName", "Person.FirstName", "en:first_name", "en")]
    [InlineData("klient_nazwisko", "Person.LastName", "pl:nazwisko", "pl")]
    [InlineData("adresEmail", "Contact.Email", "pl:adres_email", "pl")]
    [InlineData("imię", "Person.FirstName", "pl:imie", "pl")]
    [InlineData("dcd_NIPDostawcy", "Company.TaxId", "pl:nip", "pl")]
    [InlineData("customer_phone_number", "Contact.Phone", "en:phone_number", "en")]
    [InlineData("NumerTelefonu", "Contact.Phone", "pl:numer_telefonu", "pl")]
    [InlineData("billing_postal_code", "Address.PostalCode", "en:postal_code", "en")]
    public void DetectsEnglishAndPolishNames(
        string columnName,
        string expectedRole,
        string expectedRule,
        string expectedLocale)
    {
        CandidateDetectionConfiguration detection = _detector.Detect(columnName);

        Assert.True(detection.IsCandidate);
        Assert.Equal(expectedRole, detection.SuggestedRole);
        Assert.Equal(expectedRule, detection.MatchedRule);
        Assert.Equal(expectedLocale, detection.Locale);
        Assert.InRange(detection.Confidence, 0.8m, 1m);
    }

    [Theory]
    [InlineData("email_enabled")]
    [InlineData("address_type")]
    [InlineData("TaxIdRequired")]
    [InlineData("WebsiteAddress")]
    [InlineData("net_address")]
    [InlineData("description")]
    [InlineData("ClientPhoneId")]
    [InlineData("KontrolaPESEL")]
    public void RejectsFlagsAndUnrelatedNames(string columnName)
    {
        CandidateDetectionConfiguration detection = _detector.Detect(columnName);

        Assert.False(detection.IsCandidate);
        Assert.Equal(string.Empty, detection.SuggestedRole);
    }

    [Fact]
    public void TechnicalPrefixKeepsCandidateButLowersConfidence()
    {
        CandidateDetectionConfiguration exact = _detector.Detect("email");
        CandidateDetectionConfiguration prefixed = _detector.Detect("customer_email");

        Assert.True(prefixed.IsCandidate);
        Assert.Equal(exact.SuggestedRole, prefixed.SuggestedRole);
        Assert.True(prefixed.Confidence < exact.Confidence);
    }

    [Theory]
    [InlineData("PESEL", DbDataType.Integer, "Person.NationalId")]
    [InlineData("phone_number", DbDataType.Decimal, "Contact.Phone")]
    [InlineData("birth_date", DbDataType.DateTime, "Person.BirthDate")]
    [InlineData("plec", DbDataType.Boolean, "Person.Gender")]
    [InlineData("email", DbDataType.Json, "Contact.Email")]
    [InlineData("ZrodloNIP", DbDataType.Text, "Company.TaxId")]
    public void KeepsCandidatesStoredInCompatibleTypes(
        string columnName,
        DbDataType dataType,
        string expectedRole)
    {
        CandidateDetectionConfiguration detection = _detector.Detect(columnName, dataType);

        Assert.True(detection.IsCandidate);
        Assert.Equal(expectedRole, detection.SuggestedRole);
    }

    [Theory]
    [InlineData("jpr_PeselJakoNrKontrahenta", DbDataType.Boolean)]
    [InlineData("IdWojewodztwo", DbDataType.Integer)]
    [InlineData("operator_imie", DbDataType.Integer)]
    [InlineData("email", DbDataType.Integer)]
    [InlineData("telefon", DbDataType.DateTime)]
    [InlineData("TelefonZrodlo", DbDataType.Integer)]
    public void RejectsCandidatesStoredInIncompatibleTypes(string columnName, DbDataType dataType)
    {
        CandidateDetectionConfiguration detection = _detector.Detect(columnName, dataType);

        Assert.False(detection.IsCandidate);
    }
}
