namespace Aw.Evidence.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static EvidenceSettings Read(Func<string, string?> environment)
    {
        var root = Set(environment("AW_STATE_DIR"))
            ?? Path.Combine(Set(environment("HOME")) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var locks = Path.Combine(root, "locks");
        return new(
            StateDirectory: Path.Combine(root, "evidence"),
            Locks: new(
                QaStack: Set(environment("AW_QA_STACK_LOCK")) ?? Path.Combine(locks, "qa-stack.lock"),
                HeavyJob: Set(environment("AW_HEAVY_JOB_LOCK")) ?? Path.Combine(locks, "heavy-job.lock"),
                HeavyJobHeld: string.Equals(environment("AW_HEAVY_LOCK_HELD"), "1", StringComparison.Ordinal)));
    }

    private static string? Set(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
