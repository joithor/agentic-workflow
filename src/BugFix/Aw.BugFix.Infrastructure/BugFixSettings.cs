using System.Globalization;

namespace Aw.BugFix.Infrastructure;

/// <summary>BugFix's infrastructure settings, read from the environment Composition passes.</summary>
/// <param name="CheckTimeout">How long a test command may run before it is killed and counted as failing (AW_BUGFIX_CHECK_TIMEOUT_SECONDS, default 30 minutes).</param>
internal sealed record BugFixSettings(TimeSpan CheckTimeout)
{
    internal static BugFixSettings Read(Func<string, string?> environment) =>
        new(int.TryParse(environment("AW_BUGFIX_CHECK_TIMEOUT_SECONDS"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromMinutes(30));
}
