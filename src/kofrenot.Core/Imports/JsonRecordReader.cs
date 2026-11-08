using System.Text.Json;

namespace Kofrenot.Core.Imports;

/// <summary>
/// A record of an imported file: its <see cref="Number"/> in the file (from 1), and the values of
/// each key, a dotted path into nested objects (<c>publisher.name</c>). A text, a number or a
/// boolean is one value; an array gives several, in order. Null and empty values are left out.
/// </summary>
public sealed record ImportRecord(int Number, IReadOnlyDictionary<string, IReadOnlyList<string>> Values)
{
    /// <summary>The values of a key, in order; empty when the record has none.</summary>
    public IReadOnlyList<string> ValuesOf(string key) => Values.GetValueOrDefault(key) ?? [];

    /// <summary>The first value of a key; null when the record has none.</summary>
    public string? FirstOf(string key) => ValuesOf(key) is [var first, ..] ? first : null;
}

/// <summary>The records of a file, and every key they have, in the order first met: what a mapping's rules are chosen from.</summary>
public sealed record RecordFile(IReadOnlyList<ImportRecord> Records, IReadOnlyList<string> Keys);

/// <summary>Reads a <c>.json</c> file holding an array of objects (<c>[{…}, {…}]</c>), each object a record.</summary>
public static class JsonRecordReader
{
    private const char Separator = '.';

    /// <exception cref="InvalidDataException">Not JSON, or not an array of objects.</exception>
    public static RecordFile Read(Stream json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The file is not valid JSON: {ex.Message}", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The file does not hold an array of records ([{…}, {…}]).");
            }

            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var records = new List<ImportRecord>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"Record {records.Count + 1} is not an object ({{…}}).");
                }

                var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                Flatten(element, prefix: null, values);
                foreach (var key in values.Keys.Where(seen.Add))
                {
                    keys.Add(key);
                }

                records.Add(new ImportRecord(records.Count + 1, values.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal)));
            }

            return new RecordFile(records, keys);
        }
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, List<string>> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, prefix is null ? property.Name : $"{prefix}{Separator}{property.Name}", values);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, prefix, values);
                }

                break;
            default:
                if (prefix is not null && TextOf(element) is { Length: > 0 } text)
                {
                    if (!values.TryGetValue(prefix, out var list))
                    {
                        values[prefix] = list = [];
                    }

                    list.Add(text);
                }

                break;
        }
    }

    private static string? TextOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!.Trim(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };
}
