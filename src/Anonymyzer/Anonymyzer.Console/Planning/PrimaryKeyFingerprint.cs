namespace Anonymyzer.Console.Planning;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

internal static class PrimaryKeyFingerprint
{
    public static string Compute(object? value, string secret)
    {
        EnsureSecretIsValid(secret);

        string canonicalValue = value switch
        {
            null => "null",
            byte[] bytes => "bytes:" + Convert.ToBase64String(bytes),
            DateTime dateTime => "datetime:" + dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => "datetimeoffset:" +
                dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Guid guid => "guid:" + guid.ToString("D", CultureInfo.InvariantCulture),
            IFormattable formattable => value.GetType().FullName + ":" +
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.GetType().FullName + ":" + value
        };
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalValue)));
    }

    public static void EnsureSecretIsValid(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Checkpoint fingerprint secret must contain at least 32 characters.");
        }
    }
}
