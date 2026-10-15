using System.Text.Json.Serialization;

namespace Aw.Context.Core.Eval;

/// <summary>A decision a probe's answer carries.</summary>
/// <param name="Record">The canonical key of the record that holds the quote.</param>
/// <param name="Quote">Verbatim text from that record.</param>
/// <param name="Status">'current' or 'superseded'.</param>
/// <param name="ConflictsWith">The repo path whose code disagrees with the decision, or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProbeDecision(string Record, string Quote, string Status, string? ConflictsWith = null);
