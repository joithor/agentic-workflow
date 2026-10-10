using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Tests;

/// <summary>Record and class shapes that pin what the Node JSON profile does and does not guarantee.</summary>
internal static class Shapes
{
    internal sealed record ItemStarted(string Item);

    internal sealed record Scalars(
        DateTime When,
        decimal Money,
        decimal Whole,
        long Big,
        float Single,
        ulong Huge,
        double Nan,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Maybe);

    internal sealed record When(DateTime At);

    internal sealed record Wrapped(string Name, System.Text.Json.JsonElement Data, JsonNode Node);

    internal sealed record Listed(IReadOnlyList<string> Items);

    internal sealed class InitOnly
    {
        // why: shows the limit: a member that is missing from the JSON keeps its initializer, even null!.
        public string Name { get; init; } = null!;
    }

    internal sealed class RequiredInit
    {
        public required string Name { get; init; }
    }

    internal abstract record BaseShape(int A);

    internal sealed record DerivedShape(int A, int B) : BaseShape(A);

    internal abstract record OrderedBase
    {
        [JsonPropertyOrder(-1)]
        public int A { get; init; }
    }

    internal sealed record OrderedDerived(int B) : OrderedBase;
}
