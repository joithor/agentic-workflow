using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The checks of 'context.extract.graphify': SND-CONTEXT-133 for a value that isn't a whole number from 0 (no limit) up to 70,000 minutes or 10,000,000 megabytes, naming its key path.</summary>
internal static class GraphifyRules
{
    private const string Path = "context.extract.graphify";
    private const double TopMegabytes = 10_000_000;
    private const double TopMinutes = 70_000;   // CancelAfter takes at most uint.MaxValue - 1 ms, about 49.7 days

    /// <summary>The first refused value, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The error, or null when every value holds.</returns>
    public static Error? Problem(GraphifySection? section) =>
        section is null
            ? null
            : Check("timeoutMinutes", section.TimeoutMinutes, "minutes", TopMinutes) ?? Check("maxSnapshotMB", section.MaxSnapshotMB, "megabytes", TopMegabytes)
                ?? Check("maxFileMB", section.MaxFileMB, "megabytes", TopMegabytes) ?? Check("maxGraphMB", section.MaxGraphMB, "megabytes", TopMegabytes);

    /// <summary>The limits as written, with the defaults for what is missing (the section already passed <see cref="Problem"/>).</summary>
    /// <param name="section">The section, or null.</param>
    /// <returns>The limits.</returns>
    public static ClusterLimits Limits(GraphifySection? section)
    {
        var defaults = ClusterLimits.Default;
        return section is null
            ? defaults
            : new ClusterLimits
            {
                TimeoutMinutes = Whole(section.TimeoutMinutes, defaults.TimeoutMinutes),
                MaxSnapshotMB = Whole(section.MaxSnapshotMB, defaults.MaxSnapshotMB),
                MaxFileMB = Whole(section.MaxFileMB, defaults.MaxFileMB),
                MaxGraphMB = Whole(section.MaxGraphMB, defaults.MaxGraphMB),
            };
    }

    private static int Whole(double? value, int fallback) => value is { } v ? (int)v : fallback;

    private static Error? Check(string key, double? value, string unit, double top) =>
        value is { } v && (v < 0 || v > top || v != Math.Floor(v))
            ? ContextErrors.GraphifyLimitInvalid.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Path}.{key}: is a whole number of {unit}, 0 to {top:0} (0 is no limit)"))
            : null;
}
