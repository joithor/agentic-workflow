using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A decision in the intent band.</summary>
/// <param name="Node">Node, as served.</param>
/// <param name="Text">Text, as served.</param>
/// <param name="Decides">Decides, as served.</param>
/// <param name="Supersedes">Supersedes, as served.</param>
/// <param name="Quotes">Quotes, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="At">At, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Quote">Quote, as served.</param>
/// <param name="Anchors">Anchors, as served.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
internal sealed record LensDecision(
    string Node,
    string Text,
    IReadOnlyList<string> Decides,
    IReadOnlyList<string> Supersedes,
    JsonElement? Quotes,
    string Provenance,
    bool Confirmed,
    string Trust,
    DateTimeOffset At,
    string Label,
    string Kind,
    DecisionQuote? Quote = null,
    IReadOnlyList<string>? Anchors = null,
    int? Depth = null,
    string? Repo = null);
