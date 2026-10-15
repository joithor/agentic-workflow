using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Proposals;

/// <summary>Where a scoping run's surfaces stand against the map (spec §8.7): mapped, new or unmapped. Pure.</summary>
public static class GraphStatuses
{
    /// <summary>The surfaces a run proposed, rebuilt from its active 'propose' rows.</summary>
    /// <param name="run">The run.</param>
    /// <param name="overrides">Every override.</param>
    /// <returns>The surfaces, in proposal order.</returns>
    public static IReadOnlyList<ProposedSurface> ForRun(string run, IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(overrides);
        throw new NotSupportedException("S-4: GraphStatuses.ForRun is not built yet");
    }

    /// <summary>The status of each surface.</summary>
    /// <param name="surfaces">The surfaces.</param>
    /// <param name="current">The current map with its overrides applied.</param>
    /// <param name="draft">The latest draft with its overrides applied, or null when there is none.</param>
    /// <returns>One status per surface: mapped when the current map has it, new when only the draft has it, else unmapped.</returns>
    public static IReadOnlyList<GraphStatus> Of(IReadOnlyList<ProposedSurface> surfaces, GraphSnapshot current, GraphSnapshot? draft)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(current);
        _ = draft;
        throw new NotSupportedException("S-4: GraphStatuses.Of is not built yet");
    }
}
