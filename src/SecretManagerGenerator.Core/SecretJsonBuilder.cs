using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SecretManagerGenerator.Core;

public static class SecretJsonBuilder
{
    // Relaxed: deja &, ', <, > y + legibles en vez de \uXXXX; el resultado sigue siendo JSON válido.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build(IEnumerable<ConfigEntry> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var entry in entries.OrderBy(e => e.Section))
                writer.WriteString(entry.SecretKey, entry.Value);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
