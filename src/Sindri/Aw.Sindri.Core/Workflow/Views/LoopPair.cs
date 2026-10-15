using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A loop a state belongs to (mitigate and verify).</summary>
/// <param name="With">The other state.</param>
/// <param name="N">The round.</param>
/// <param name="Max">The cap.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoopPair(
    string With,
    int N,
    int Max);
