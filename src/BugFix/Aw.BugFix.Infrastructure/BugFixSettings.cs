using System.Globalization;

namespace Aw.BugFix.Infrastructure;

/// <summary>BugFix's infrastructure settings, read from the environment Composition passes.</summary>
/// <param name="CheckTimeout">How long a test command may run before it is killed and counted as not run (AW_BUGFIX_CHECK_TIMEOUT_SECONDS, default 30 minutes).</param>
/// <param name="LockWait">How long a call waits for another call's state lock before giving up (AW_BUGFIX_LOCK_WAIT_SECONDS, default 30 seconds).</param>
/// <param name="Path">The PATH a test command's program is looked up on (the process's own PATH).</param>
/// <param name="FileLockingDisabled">Whether DOTNET_SYSTEM_IO_DISABLEFILELOCKING turns .NET's file locks off, which would let two calls write the state at once.</param>
internal sealed record BugFixSettings(TimeSpan CheckTimeout, TimeSpan LockWait, string? Path = null, bool FileLockingDisabled = false)
{
    internal static BugFixSettings Read(Func<string, string?> environment) =>
        new(Seconds(environment("AW_BUGFIX_CHECK_TIMEOUT_SECONDS")) ?? TimeSpan.FromMinutes(30),
            Seconds(environment("AW_BUGFIX_LOCK_WAIT_SECONDS")) ?? TimeSpan.FromSeconds(30),
            environment("PATH"),
            Enabled(environment("DOTNET_SYSTEM_IO_DISABLEFILELOCKING")));

    private static bool Enabled(string? flag) =>
        string.Equals(flag, "1", StringComparison.Ordinal) || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan? Seconds(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
}
