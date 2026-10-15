using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>The override history read as a log (S-4): oldest first, with the undone rows and the undo rows themselves set aside.</summary>
public static class OverrideLog
{
    /// <summary>The overrides in application order: by time, then by id.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The same rows, ordered.</returns>
    public static IReadOnlyList<MapOverride> Ordered(IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        return [.. overrides.Select(o => (Override: o, Id: o.ToId().Value)).OrderBy(x => x.Override.At).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Override)];
    }

    /// <summary>The ids of the overrides an effective undo reverses. An undo that is itself undone reverses nothing, so the row it named counts again.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The reversed ids.</returns>
    public static IReadOnlySet<string> Undone(IReadOnlyList<MapOverride> overrides)
    {
        var undone = new HashSet<string>(StringComparer.Ordinal);
        foreach (var undo in Ordered(overrides).Reverse().Where(o => o is { Action: OverrideAction.Undo, Undoes: not null }))
        {
            if (!undone.Contains(undo.ToId().Value))
            {
                _ = undone.Add(undo.Undoes!.Value.Value);   // why: the filter above keeps only rows with an Undoes id
            }
        }

        return undone;
    }

    /// <summary>The overrides that still act: not reversed, and not undo rows.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The rows, in application order.</returns>
    public static IReadOnlyList<MapOverride> Active(IReadOnlyList<MapOverride> overrides)
    {
        var undone = Undone(overrides);
        return [.. Ordered(overrides).Where(o => o.Action != OverrideAction.Undo && !undone.Contains(o.ToId().Value))];
    }

    /// <summary>Whether an override decides a proposal: an accept, a reject, a merge or a delete. A rename or a move doesn't.</summary>
    /// <param name="o">The override.</param>
    /// <returns>True when it decides.</returns>
    public static bool Decides(MapOverride o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return o.Action is OverrideAction.Accept or OverrideAction.Reject or OverrideAction.Merge or OverrideAction.Delete;
    }
}
