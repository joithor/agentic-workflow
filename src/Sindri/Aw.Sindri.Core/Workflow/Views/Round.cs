using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A round counter (r2/3).</summary>
/// <param name="N">The current round.</param>
/// <param name="Max">The cap.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Round(
    int N,
    int Max);
