namespace Anonymyzer.Generators.Simple;

internal sealed class JsonRedactionPath
{
    private JsonRedactionPath(string value, IReadOnlyList<Segment> segments)
    {
        Value = value;
        Segments = segments;
    }

    public string Value { get; }

    public IReadOnlyList<Segment> Segments { get; }

    public static bool TryParse(string? value, out JsonRedactionPath? path, out string error)
    {
        path = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value[0] != '$')
        {
            error = "Path must start with '$'.";
            return false;
        }

        var segments = new List<Segment>();
        int position = 1;
        while (position < value.Length)
        {
            if (value.AsSpan(position).StartsWith("[]", StringComparison.Ordinal))
            {
                segments.Add(new Segment(SegmentKind.AllArrayItems, string.Empty));
                position += 2;
                continue;
            }

            if (value[position] != '/')
            {
                error = $"Unexpected character at position {position + 1}. Expected '/' or '[].'";
                return false;
            }

            position++;
            int propertyStart = position;
            while (position < value.Length && value[position] != '/' && value[position] != '[')
            {
                position++;
            }

            string encodedProperty = value[propertyStart..position];
            if (!TryDecodeProperty(encodedProperty, out string propertyName))
            {
                error = $"Property '{encodedProperty}' contains an invalid JSON Pointer escape.";
                return false;
            }

            segments.Add(new Segment(SegmentKind.Property, propertyName));
            if (position < value.Length && value[position] == '[')
            {
                if (!value.AsSpan(position).StartsWith("[]", StringComparison.Ordinal))
                {
                    error = $"Only the array wildcard '[]' is supported at position {position + 1}.";
                    return false;
                }

                segments.Add(new Segment(SegmentKind.AllArrayItems, string.Empty));
                position += 2;
            }
        }

        path = new JsonRedactionPath(value, segments);
        return true;
    }

    public bool IsPrefixOf(JsonRedactionPath other)
    {
        if (Segments.Count >= other.Segments.Count)
        {
            return false;
        }

        return Segments.SequenceEqual(other.Segments.Take(Segments.Count));
    }

    private static bool TryDecodeProperty(string encoded, out string decoded)
    {
        var result = new System.Text.StringBuilder(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] != '~')
            {
                result.Append(encoded[index]);
                continue;
            }

            if (++index >= encoded.Length || encoded[index] is not ('0' or '1'))
            {
                decoded = string.Empty;
                return false;
            }

            result.Append(encoded[index] == '0' ? '~' : '/');
        }

        decoded = result.ToString();
        return true;
    }

    internal sealed record Segment(SegmentKind Kind, string PropertyName);

    internal enum SegmentKind
    {
        Property,
        AllArrayItems
    }
}
