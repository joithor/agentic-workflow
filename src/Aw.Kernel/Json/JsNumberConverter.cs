using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>
/// Numbers as Node holds them: every JS number is a double, so the value is written as the double nearest to it,
/// laid out like Number#toString (<see cref="JsNumber"/>). A long above 2^53 therefore writes the same rounded
/// digits Node would. Reading parses the number text into <typeparamref name="T"/> exactly and fails when it
/// doesn't fit (a fraction for an integer, out of range). For double and float, NaN and the infinities are written
/// as null (as JSON.stringify does) and null reads back as NaN.
/// </summary>
/// <typeparam name="T">double, float, decimal, long or ulong.</typeparam>
internal sealed class JsNumberConverter<T> : JsonConverter<T>
    where T : struct, INumberBase<T>
{
    private static readonly bool _isFloatingPoint = typeof(T) == typeof(double) || typeof(T) == typeof(float);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null && _isFloatingPoint)
        {
            return T.CreateTruncating(double.NaN);
        }

        return reader.TokenType == JsonTokenType.Number
            && T.TryParse(reader.ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new JsonException($"Expected a number that fits {typeof(T).Name}, found {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteRawValue(JsNumber.Format(double.CreateChecked(value)), skipInputValidation: true);
}
