using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>JSON value equality: numbers by value, objects without regard to member order, arrays in order.</summary>
internal static class JsonEquality
{
    internal static bool Equal(JsonElement left, JsonElement right) =>
        left.ValueKind == right.ValueKind
        && left.ValueKind switch
        {
            JsonValueKind.Number => NumbersEqual(left, right),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength() && left.EnumerateArray().Zip(right.EnumerateArray()).All(static pair => Equal(pair.First, pair.Second)),
            JsonValueKind.Object => ObjectsEqual(left, right),
            JsonValueKind.Undefined or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => false,
        };

    private static bool NumbersEqual(JsonElement left, JsonElement right) =>
        left.TryGetDecimal(out var a) && right.TryGetDecimal(out var b) ? a == b : left.GetDouble().Equals(right.GetDouble());

    private static bool ObjectsEqual(JsonElement left, JsonElement right)
    {
        var members = left.EnumerateObject().ToList();
        return members.Count == right.EnumerateObject().ToList().Count && members.TrueForAll(m => right.TryGetProperty(m.Name, out var other) && Equal(m.Value, other));
    }
}
