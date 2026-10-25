using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One entry of the review queue.</summary>
/// <param name="Node">Node, as served.</param>
/// <param name="Link">Link, as served.</param>
/// <param name="Group">Group, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Provenance">Provenance, as served.</param>
/// <param name="Confidence">Confidence, as served.</param>
/// <param name="Impact">Impact, as served.</param>
/// <param name="Files">Files, as served.</param>
/// <param name="Parent">Parent, as served.</param>
/// <param name="Trust">Trust, as served.</param>
/// <param name="Override">Override, as served.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
internal sealed record ReviewEntry(
    string Node,
    string? Link,
    string Group,
    string Label,
    string Provenance,
    double Confidence,
    int Impact,
    IReadOnlyList<string> Files,
    string? Parent,
    string Trust,
    string? Override = null,
    string? Repo = null)
{
    /// <summary>Whether the group is one this build knows; any other word is drawn raw.</summary>
    [JsonIgnore]
    public bool HasKnownGroup => ContextWords.Known(ContextWords.Groups, Group);
}
