namespace Aw.Sindri.Core.Scheduling;

/// <summary>The heads the context map is built from, for the move check: a pure function of stored map state.</summary>
public static class MappedHeads
{
    /// <summary>The held draft's heads if there is a hold, else the current version's, else none. A draft that is not held does not count; a version with no recorded heads reads as none.</summary>
    /// <param name="hold">The hold, or null.</param>
    /// <param name="currentHeads">The current version's commit by repo name, or null when there is none.</param>
    /// <returns>The commit by repo name, or null for none (which gets one refresh).</returns>
    public static IReadOnlyDictionary<string, string>? Of(MapHoldState? hold, IReadOnlyDictionary<string, string>? currentHeads)
    {
        var heads = hold is null ? currentHeads : hold.Heads.ToDictionary(static h => h.Key, static h => h.Value.Commit, StringComparer.Ordinal);
        return heads is { Count: > 0 } ? heads : null;
    }
}
