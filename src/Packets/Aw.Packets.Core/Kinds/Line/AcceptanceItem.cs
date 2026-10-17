using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// One acceptance item. The fields every station relies on are typed; the rest (form, surfaces, citations, trust,
/// the item's own parent) pass through unchanged, since the scoping spec (§8.3) and Sindri's LC-ACC-001 own them.
/// </summary>
public sealed record AcceptanceItem
{
    /// <summary>The item id; never changes.</summary>
    public required ItemId Id { get; init; }

    /// <summary>The item's text; never changes.</summary>
    public required string Text { get; init; }

    /// <summary>failing, passing or superseded.</summary>
    public required AcceptanceStatus Status { get; init; }

    /// <summary>The evidence for a passing item; empty otherwise.</summary>
    public required IReadOnlyList<EvidenceRef> Evidence { get; init; }

    /// <summary>The other fields, as written.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Other { get; init; }
}
