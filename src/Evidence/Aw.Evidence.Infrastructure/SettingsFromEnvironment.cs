using System.Globalization;

namespace Aw.Evidence.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    /// <summary>The heavy-job slot count when AW_HEAVY_SLOTS is unset or invalid; the heavy wrapper's default too.</summary>
    internal const int DefaultHeavyJobSlots = 2;

    internal static EvidenceSettings Read(Func<string, string?> environment)
    {
        var slots = Set(environment("AW_HEAVY_SLOTS"));
        var slotCount = HeavyJobSlots(slots);
        // why: with neither AW_STATE_DIR nor HOME set (launchd, a stripped env) the user profile is the only sane root,
        // and it must be the same lock directory the shell tools use; a test pins AW_STATE_DIR or the lock paths instead.
        var root = Set(environment("AW_STATE_DIR"))
            ?? Path.Combine(Set(environment("HOME")) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var locks = Path.Combine(root, "locks");
        return new(
            Locks: new(
                QaStack: LockPath(environment("AW_QA_STACK_LOCK")) ?? Path.Combine(locks, "qa-stack.lock"),
                HeavyJob: LockPath(environment("AW_HEAVY_JOB_LOCK")) ?? Path.Combine(locks, "heavy-job.lock"),
                HeavyJobHeld: string.Equals(environment("AW_HEAVY_LOCK_HELD"), "1", StringComparison.Ordinal),
                HeavyJobSlots: slotCount ?? DefaultHeavyJobSlots,
                IgnoredHeavyJobSlots: slots is not null && slotCount is null ? slots : null));
    }

    private static int? HeavyJobSlots(string? value) =>
        value is not null && int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var slots) && slots >= 1 ? slots : null;

    // why: '/x/qa.lock/' would make the lock its own parent and put slot 2 inside slot 1 ('/x/qa.lock/.2').
    private static string? LockPath(string? value) => Set(value) is { } path ? Path.TrimEndingDirectorySeparator(path) : null;

    private static string? Set(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
