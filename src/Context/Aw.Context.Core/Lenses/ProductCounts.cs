namespace Aw.Context.Core.Lenses;

/// <summary>How many nodes the product band holds before the cut.</summary>
/// <param name="Own">In the own scope.</param>
/// <param name="Affects">In the cascade, outside the own scope and the neighbors.</param>
public sealed record ProductCounts(int Own, int Affects)
{
    /// <summary>In the neighbors list before the cut (the list shows at most 100), or null when the answer did not count them.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Neighbors { get; init; }

    /// <summary>In the product edges list before the cut, or null when the answer did not count them.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Edges { get; init; }
}
