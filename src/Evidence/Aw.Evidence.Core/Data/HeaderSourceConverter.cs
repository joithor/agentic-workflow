using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads a header source. A JSON string (a literal value) is refused so no secret sits in a script; an object is read by its shape.</summary>
internal sealed class HeaderSourceConverter : JsonConverter<HeaderSource>
{
    public override HeaderSource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw JsonInputProblem.At("$", "a header value must be { \"env\": \"NAME\" } naming an environment variable, never a literal value");
        }

        string? env = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            _ = reader.Read();
            if (!string.Equals(name, "env", StringComparison.Ordinal))
            {
                throw JsonInputProblem.At($"$.{name}", "unknown property");
            }

            env = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw JsonInputProblem.At("$.env", "must be a string");
        }

        return env is null ? throw JsonInputProblem.At("$", "missing required property \"env\"") : new HeaderSource(env);
    }

    public override void Write(Utf8JsonWriter writer, HeaderSource value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("env", value.Env);
        writer.WriteEndObject();
    }
}
