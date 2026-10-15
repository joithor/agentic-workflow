using System.Text.Json.Serialization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Map;

/// <summary>What 'context map status' shows.</summary>
/// <param name="Version">The current version.</param>
/// <param name="Counts">Its counts (a session's: only the nodes its repo implements and what lies between them).</param>
/// <param name="Orphaned">Overrides whose target no longer resolves.</param>
/// <param name="Retained">How many earlier versions are kept.</param>
/// <param name="ScopedTo">The repo the counts are scoped to, or null for the whole map.</param>
/// <param name="Coverage">With an anchor: the coverage of its own scope, the same record 'context lenses' serves; left out otherwise.</param>
public sealed record MapStatusView(
    MapVersion Version,
    MapCounts Counts,
    int Orphaned,
    int Retained,
    ContextRepoName? ScopedTo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LensCoverage? Coverage = null);
