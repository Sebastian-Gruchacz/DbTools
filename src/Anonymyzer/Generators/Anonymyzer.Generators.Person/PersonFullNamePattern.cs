namespace Anonymyzer.Generators.Person;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[JsonConverter(typeof(StringEnumConverter))]
public enum PersonFullNamePattern
{
    FirstNameLastName,
    LastNameFirstName
}
