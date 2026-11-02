using System.Text.Json;
using System.Text.Json.Serialization;
using Pexilio.Application;

namespace Pexilio.Api;

public sealed class OptionalTextConverter : JsonConverter<OptionalText>
{
    public override OptionalText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new OptionalText { Specified = true, Value = null };
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A price must be a string or null.");
        }

        return new OptionalText { Specified = true, Value = reader.GetString() };
    }

    public override void Write(Utf8JsonWriter writer, OptionalText value, JsonSerializerOptions options)
    {
        if (value.Value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
