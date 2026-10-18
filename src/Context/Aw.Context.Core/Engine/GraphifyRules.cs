using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The checks of 'context.extract.graphify': SND-CONTEXT-133 for a value that isn't a whole number from 0 (no limit) to 10,000,000, naming its key path.</summary>
internal static class GraphifyRules
{
    private const string Path = "context.extract.graphify";
    private const double Top = 10_000_000;

    /// <summary>The first refused value, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The error, or null when every value holds.</returns>
    public static Error? Problem(GraphifySection? section) =>
        section is null
            ? null
            : Check("timeoutMinutes", section.TimeoutMinutes) ?? Check("maxSnapshotMB", section.MaxSnapshotMB) ?? Check("maxFileMB", section.MaxFileMB) ?? Check("maxGraphMB", section.MaxGraphMB);

    /// <summary>The limits as written, with the defaults for what is missing (the section already passed <see cref="Problem"/>).</summary>
    /// <param name="section">The section, or null.</param>
    /// <returns>The limits.</returns>
    public static GraphifyLimits Limits(GraphifySection? section)
    {
        var defaults = GraphifyLimits.Default;
        return section is null
            ? defaults
            : new GraphifyLimits
            {
                TimeoutMinutes = Whole(section.TimeoutMinutes, defaults.TimeoutMinutes),
                MaxSnapshotMB = Whole(section.MaxSnapshotMB, defaults.MaxSnapshotMB),
                MaxFileMB = Whole(section.MaxFileMB, defaults.MaxFileMB),
                MaxGraphMB = Whole(section.MaxGraphMB, defaults.MaxGraphMB),
            };
    }

    private static int Whole(double? value, int fallback) => value is { } v ? (int)v : fallback;

    private static Error? Check(string key, double? value) =>
        value is { } v && (v < 0 || v > Top || v != Math.Floor(v))
            ? ContextErrors.GraphifyLimitInvalid.With($"{Path}.{key}: is a whole number of {(key.EndsWith("Minutes", StringComparison.Ordinal) ? "minutes" : "megabytes")}, 0 or more (0 is no limit)")
            : null;
}
