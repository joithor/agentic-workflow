namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Parses and formats the sizes, percentages and durations of the profile's sandbox section (already checked by the profile rules).</summary>
public static class SandboxUnits
{
    /// <summary>Parses a size such as '256MiB' or '12GiB'.</summary>
    /// <param name="size">The size text.</param>
    /// <returns>Bytes.</returns>
    public static long ParseSize(string size) => throw new NotSupportedException();

    /// <summary>Formats bytes the way the summary shows them: whole GiB, else whole MiB.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>For example '12GiB'.</returns>
    public static string FormatSize(long bytes) => throw new NotSupportedException();

    /// <summary>Parses '50%' to 50.</summary>
    /// <param name="percent">The percent text.</param>
    /// <returns>The number.</returns>
    public static int ParsePercent(string percent) => throw new NotSupportedException();

    /// <summary>Formats a duration as whole hours ('4h') or minutes ('70m').</summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The text.</returns>
    public static string FormatDuration(TimeSpan duration) => throw new NotSupportedException();
}
