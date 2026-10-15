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
        ArgumentNullException.ThrowIfNull(overrides);
        return [.. OverrideLog.Active(overrides)
            .Where(o => o.Action == OverrideAction.Propose && o.Proposal is { } p && p.Run == run)
            .Select(o => new ProposedSurface(o.Label ?? o.Target.Name, o.Target, o.Proposal!.Kind, o.Proposal.Parent, o.Proposal.Evidence, o.Proposal.Quote))];   // why: the filter keeps only rows with a payload
    }

    /// <summary>
    /// Every surface a run submitted: the list the run saved (the mapped and unmapped ones too), then any surface of it that only a
    /// 'propose' row remembers (a run saved before the list was kept), each once.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="saved">The surfaces the run saved, or null when none were.</param>
    /// <returns>The surfaces, in submission order.</returns>
    public static IReadOnlyList<ProposedSurface> ForRun(string run, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ProposedSurface>? saved)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var surfaces = new List<ProposedSurface>(saved ?? []);
        var known = surfaces.Select(Identity).ToHashSet(StringComparer.Ordinal);
        surfaces.AddRange(ForRun(run, overrides).Where(s => known.Add(Identity(s))));
        return surfaces;
    }

    // What makes two submissions of a surface the same: its node key when it has one, else its name.
    private static string Identity(ProposedSurface surface) => ProposalNodes.KeyOf(surface) is { } key ? key.Value : "name:" + surface.Name;

    /// <summary>The status of each surface.</summary>
    /// <param name="surfaces">The surfaces.</param>
    /// <param name="current">The current map with its overrides applied.</param>
    /// <param name="draft">The latest draft with its overrides applied, or null when there is none.</param>
    /// <returns>One status per surface: mapped when the current map has it, new when only the draft has it, else unmapped.</returns>
    public static IReadOnlyList<GraphStatus> Of(IReadOnlyList<ProposedSurface> surfaces, GraphSnapshot current, GraphSnapshot? draft)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(current);
        return [.. surfaces.Select(s =>
        {
            var key = ProposalNodes.KeyOf(s);
            if (key is null)
            {
                return new GraphStatus(s.Name, Node: null, GraphStatusKind.Unmapped);
            }

            if (current.Resolve(key.Value) is { } mapped)
            {
                return new GraphStatus(s.Name, mapped.Key, GraphStatusKind.Mapped);
            }

            return draft?.Resolve(key.Value) is { } proposed
                ? new GraphStatus(s.Name, proposed.Key, GraphStatusKind.New)
                : new GraphStatus(s.Name, Node: null, GraphStatusKind.Unmapped);
        })];
    }
}
