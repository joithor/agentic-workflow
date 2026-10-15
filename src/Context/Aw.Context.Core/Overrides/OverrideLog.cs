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
        throw new NotSupportedException("S-4: OverrideLog.Ordered is not built yet");
    }

    /// <summary>The ids of the overrides an effective undo reverses. An undo that is itself undone reverses nothing, so the row it named counts again.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The reversed ids.</returns>
    public static IReadOnlySet<string> Undone(IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        throw new NotSupportedException("S-4: OverrideLog.Undone is not built yet");
    }

    /// <summary>The overrides that still act: not reversed, and not undo rows.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The rows, in application order.</returns>
    public static IReadOnlyList<MapOverride> Active(IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        throw new NotSupportedException("S-4: OverrideLog.Active is not built yet");
    }

    /// <summary>Whether an override decides a proposal: an accept, a reject, a merge or a delete. A rename or a move doesn't.</summary>
    /// <param name="o">The override.</param>
    /// <returns>True when it decides.</returns>
    public static bool Decides(MapOverride o)
    {
        ArgumentNullException.ThrowIfNull(o);
        throw new NotSupportedException("S-4: OverrideLog.Decides is not built yet");
    }
}
