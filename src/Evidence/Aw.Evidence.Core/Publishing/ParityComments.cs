using System.Globalization;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Markdown for a parity run: a Linear comment (images live on Linear) and a GitHub comment (text only).</summary>
public static class ParityComments
{
    /// <summary>The Linear comment: build, viewport, design node, and per frame the diff, geometry and known differences.</summary>
    /// <param name="summary">The parity run.</param>
    /// <returns>The markdown.</returns>
    public static string Linear(ParitySummary summary)
    {
        var rows = summary.Frames.Select(static f => Invariant(
            $"| {f.Name} | {f.DiffPercent}% ({f.DiffPixels}/{f.Total}) | {Geometry(f)} | {Known(f)} |"));
        return string.Join('\n',
        [
            $"**Design parity** — build {summary.AppBuild ?? "unverified"}, viewport {summary.Viewport.Describe()}, design node {summary.DesignNode ?? "unspecified"}",
            string.Empty,
            "| State | Pixel diff | Geometry | Known differences |",
            "|---|---|---|---|",
            .. rows,
        ]);
    }

    /// <summary>The GitHub comment: numbers and explanations, never image URLs.</summary>
    /// <param name="summary">The parity run.</param>
    /// <returns>The markdown.</returns>
    public static string GitHub(ParitySummary summary)
    {
        var rows = summary.Frames.Select(static f => Invariant($"| {f.Name} | {f.DiffPercent}% | {Known(f)} |"));
        var failures = summary.FailedBoxChecks();
        string[] tail = failures.Count == 0
            ? []
            : [string.Empty, $"Geometry failures: {string.Join("; ", failures.Select(static x => $"{x.Frame}: {x.Check.Target} ({x.Check.Failure})"))}"];
        return string.Join('\n',
        [
            $"## Design parity ({Name(summary.Provenance)} data, images on Linear)",
            string.Empty,
            "| State | Pixel diff | Known differences |",
            "|---|---|---|",
            .. rows,
            .. tail,
        ]);
    }

    private static string Geometry(ParityFrame f) => f.BoxChecks.Count == 0 ? "—" : f.BoxChecks.All(static c => c.Pass) ? "pass" : "FAIL";

    private static string Known(ParityFrame f) => f.KnownDifferences.Count > 0 ? string.Join("; ", f.KnownDifferences) : "—";

    private static string Name(Provenance provenance) => provenance switch
    {
        Provenance.Seeded => "seeded",
        Provenance.Scrubbed => "scrubbed",
        Provenance.Unknown => "unknown",
        _ => "unknown",
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
