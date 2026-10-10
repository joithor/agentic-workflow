using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Architecture.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: the converter <see cref="HandConvertedId"/> names.</summary>
internal sealed class HandIdConverter : JsonConverter<HandConvertedId>
{
    public override HandConvertedId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        HandConvertedId.From(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, HandConvertedId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
