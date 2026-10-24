using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Desktop;

/// <summary>A desktop-run's record, written to &lt;run-dir&gt;/summary.json (schema 'ui-evidence.desktop/1'). Every key is always written.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Kind">Always 'desktop'.</param>
/// <param name="RunId">The run (the run directory's name).</param>
/// <param name="StartedAt">When the run started (UTC).</param>
/// <param name="FinishedAt">When it finished (UTC).</param>
/// <param name="AppBuild">The --app-build commit, when given.</param>
/// <param name="ManifestSha256">SHA-256 of the screens manifest.</param>
/// <param name="Outcome">How the run ended; agrees with the exit code.</param>
/// <param name="Headline">One line, 200 characters at most, scrubbed.</param>
/// <param name="Screens">Every screen at every size, in manifest order.</param>
/// <param name="Refusal">Set when a guard refused the run (exit 1).</param>
/// <param name="Error">Set when the run errored (exit 4).</param>
public sealed record DesktopSummary(
    string Schema,
    string Kind,
    RunId RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? AppBuild,
    string ManifestSha256,
    DesktopOutcome Outcome,
    string Headline,
    IReadOnlyList<DesktopScreenResult> Screens,
    CodedMessage? Refusal,
    CodedMessage? Error)
{
    /// <summary>The schema this build writes.</summary>
    public const string SchemaName = "ui-evidence.desktop/1";

    /// <summary>The 'kind' a desktop summary carries.</summary>
    public const string KindName = "desktop";

    /// <summary>The process exit code this summary's outcome stands for.</summary>
    /// <returns>0 passed, 2 failed, 1 refused, 4 errored.</returns>
    public int ExitCode() => Outcome switch
    {
        DesktopOutcome.Passed => 0,
        DesktopOutcome.Failed => 2,
        DesktopOutcome.Refused => 1,
        _ => 4,
    };
}

/// <summary>One screen at one window size: the pixel verdict and where its images are.</summary>
/// <param name="Name">The screen.</param>
/// <param name="Size">The window size, 'WxH'.</param>
/// <param name="DiffPercent">The share of pixels that differ, in percent (two decimals); 0 when the frames could not be compared.</param>
/// <param name="Threshold">The limit it was judged by, in percent.</param>
/// <param name="Passed">True when the frames compared and the difference is within the limit.</param>
/// <param name="Problem">Why the screen failed beyond the difference (a capture of another size, an unreadable image); null otherwise.</param>
/// <param name="Files">The screen's images (absolute paths).</param>
/// <param name="Accepted">The differences the manifest lists as accepted; the review step skips them.</param>
/// <param name="Masked">How many mask regions were painted over.</param>
public sealed record DesktopScreenResult(
    string Name,
    string Size,
    double DiffPercent,
    double Threshold,
    bool Passed,
    string? Problem,
    DesktopFiles Files,
    IReadOnlyList<string> Accepted,
    int Masked);

/// <summary>The images of one screen at one size.</summary>
/// <param name="App">The app's frame.</param>
/// <param name="Design">The mockup's frame.</param>
/// <param name="Diff">The pixel diff.</param>
/// <param name="SideBySide">Design, app and diff in one image.</param>
public sealed record DesktopFiles(string App, string Design, string Diff, string SideBySide)
{
    /// <summary>The images of a screen at a size, below the run directory.</summary>
    /// <param name="runDirectory">The run directory (absolute).</param>
    /// <param name="name">The screen.</param>
    /// <param name="size">The window size, 'WxH'.</param>
    /// <returns>The paths.</returns>
    public static DesktopFiles For(string runDirectory, string name, string size)
    {
        var folder = runDirectory.TrimEnd('/') + "/" + name + "-" + size + "/";
        return new DesktopFiles(folder + "app.png", folder + "design.png", folder + "diff.png", folder + "side-by-side.png");
    }
}
