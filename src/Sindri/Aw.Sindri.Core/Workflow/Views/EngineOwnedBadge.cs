using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The badge on an interrupted state (E-20).</summary>
/// <param name="Kind">Which badge.</param>
/// <param name="Reason">Why.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EngineOwnedBadge(
    EngineOwnedKind Kind,
    string Reason);
