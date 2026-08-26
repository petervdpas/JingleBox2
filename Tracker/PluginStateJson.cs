using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// Reads and writes a plugin's state as base64 text, and treats text that is not base64 as no
/// state rather than as a broken file.
/// </summary>
/// <remarks>
/// The serializer's own byte array converter throws on the first character it cannot read, and
/// a throw here does not cost the plugin its patch: it costs the whole file, because one bad
/// character anywhere in a document is the whole document. A rack instrument would vanish from
/// the rack, and before songs were containers a damaged patch took the patterns with it.
///
/// So a state that will not read is no state. The instrument still opens, on the plugin's own
/// defaults, which is the same thing that happens when a plugin has never been given one.
/// </remarks>
public sealed class PluginStateJson : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) return Array.Empty<byte>();

        try
        {
            return reader.GetBytesFromBase64();
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        if (value == null || value.Length == 0) writer.WriteStringValue("");
        else writer.WriteBase64StringValue(value);
    }
}
