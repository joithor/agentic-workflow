namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>Where the heavy-job lock lives and how many slots it has (config/lib/locks.sh, the heavy wrapper and Evidence's directory locks read the same variables).</summary>
/// <param name="Lock">The lock's slot 1: AW_HEAVY_JOB_LOCK, or &lt;state root&gt;/locks/heavy-job.lock. Slot k is '&lt;Lock&gt;.k'.</param>
/// <param name="Slots">AW_HEAVY_SLOTS: how many heavy jobs may run at once (default 2 when unset or empty; a value that is not a whole number of at least 1 means 1, as config/lib/locks.sh reads it).</param>
/// <param name="Held">AW_HEAVY_LOCK_HELD=1: a parent process (the heavy wrapper) already holds a slot, so taking one is a no-op.</param>
internal sealed record HeavyLockSettings(string Lock, int Slots, bool Held)
{
    /// <summary>The slots Evidence and the shell tools default to.</summary>
    internal const int DefaultSlots = 2;

    /// <summary>Reads the settings from the environment, the way config/lib/locks.sh reads the same variables: an unset or empty AW_HEAVY_SLOTS is the default, a malformed one is a single slot.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    /// <returns>The settings.</returns>
    internal static HeavyLockSettings Read(Func<string, string?> environment, string stateRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var path = environment("AW_HEAVY_JOB_LOCK") is { Length: > 0 } explicitLock ? explicitLock : Path.Combine(stateRoot, "locks", "heavy-job.lock");
        var slots = SlotCount(environment("AW_HEAVY_SLOTS"));
        return new HeavyLockSettings(Path.GetFullPath(path), slots, string.Equals(environment("AW_HEAVY_LOCK_HELD"), "1", StringComparison.Ordinal));
    }

    private static int SlotCount(string? text) =>
        string.IsNullOrEmpty(text) ? DefaultSlots
        : int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1 ? n
        : 1;
}
