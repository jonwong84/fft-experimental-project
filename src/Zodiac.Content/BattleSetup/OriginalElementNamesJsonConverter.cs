using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

/// <summary>Preserves the prepared schema's original element-name strings while runtime elements are boolean sets.</summary>
internal sealed class OriginalElementNamesJsonConverter : JsonConverter<ElementToggles>
{
    public override bool HandleNull => true;

    public override ElementToggles Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected original element-name string.");
        try { return OriginalElementEncoding.FromNames(reader.GetString()!); }
        catch (FormatException exception) { throw new JsonException(exception.Message, exception); }
    }

    public override void Write(Utf8JsonWriter writer, ElementToggles value, JsonSerializerOptions options) =>
        writer.WriteStringValue(OriginalElementEncoding.ToNames(value));
}
