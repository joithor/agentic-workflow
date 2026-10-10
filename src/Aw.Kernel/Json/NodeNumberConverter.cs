using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>Writes doubles as Node prints numbers (<see cref="JsNumber"/>); NaN and the infinities become null.</summary>
internal sealed class NodeNumberConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteRawValue(JsNumber.Format(value), skipInputValidation: true);
}
