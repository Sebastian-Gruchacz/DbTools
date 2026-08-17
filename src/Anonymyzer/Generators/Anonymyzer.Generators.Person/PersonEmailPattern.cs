namespace Anonymyzer.Generators.Person;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[JsonConverter(typeof(StringEnumConverter))]
public enum PersonEmailPattern
{
    NameBased,
    Opaque
}
