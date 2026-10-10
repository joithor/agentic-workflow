using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Model;

/// <summary>An acceptance example and the evidence kinds (addendum §17.11) that prove it.</summary>
public sealed record AcceptanceExample
{
    /// <summary>The example id.</summary>
    public required ExampleId Id { get; init; }

    /// <summary>The example, given-when-then or prose.</summary>
    public required string Text { get; init; }

    /// <summary>The evidence kinds that prove it, for example 'test' or 'cli-transcript'.</summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    /// <summary>True for a held-out example (the implementer must not read it).</summary>
    public bool Holdout { get; init; }
}
