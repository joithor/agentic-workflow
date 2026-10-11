using System.Globalization;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Visual;

namespace Aw.Evidence.Core.Publishing;

/// <summary>The UI-evidence PR comment for a run: the verdict (flagged when anything needs a look), the step table and the evidence.</summary>
public static class EvidenceComment
{
    /// <summary>Renders the comment.</summary>
    /// <param name="summary">The run.</param>
    /// <param name="note">A note for the reviewer (why evidence stayed local, an upload failure), or null.</param>
    /// <param name="urls">Uploaded URLs by local path; a path without one is shown as is.</param>
    /// <returns>The markdown.</returns>
    public static string Render(RunSummary summary, string? note, IReadOnlyDictionary<string, string> urls)
    {
        string Where(string path) => urls.TryGetValue(path, out var url) ? url : path;
        var rows = summary.Steps.Select(s => $"| {ScriptViewports.NameOf(s.Viewport)}: {Action(s)} {s.Target} | {Status(s.Status)} | {Where(s.Screenshot)} |");
        string[] evidence = [.. summary.Evidence.Traces, .. summary.Evidence.Videos, .. OptionalPath(summary.Visual.DiffImage)];
        string[] lines =
        [
            $"## UI evidence — visual: {VerdictName(summary.Visual.Verdict)}{(NeedsLook(summary) ? " ⚠" : string.Empty)}",
            string.Empty,
            "| Step | Status | Screenshot |",
            "|---|---|---|",
            .. rows,
            .. summary.Visual.DiffScore is { } score ? [string.Create(CultureInfo.InvariantCulture, $"Baseline diff: {score * 100:F3}% of pixels")] : Array.Empty<string>(),
            .. summary.Visual.Reasons.Count > 0 ? [$"Visual reasons: {string.Join("; ", summary.Visual.Reasons)}"] : Array.Empty<string>(),
            .. evidence.Length > 0 ? [string.Empty, "Evidence:", .. evidence.Select(p => $"- {Where(p)}")] : Array.Empty<string>(),
            .. note is null ? Array.Empty<string>() : [string.Empty, note],
        ];
        return string.Join('\n', lines);
    }

    /// <summary>Unclear, looks-off or sloppy verdicts, failed steps and lint findings all ask for a reviewer's look.</summary>
    /// <param name="summary">The run.</param>
    /// <returns>Whether to flag it.</returns>
    public static bool NeedsLook(RunSummary summary) =>
        summary.Visual.Verdict is not (Verdict.LooksRight or Verdict.Unchanged)
        || !summary.AllStepsPassed()
        || summary.LintFindings.Count > 0;

    private static string[] OptionalPath(string? path) => path is null ? [] : [path];

    private static string Action(RunStep s) => ScriptStep.NameOf(s.Action);

    private static string Status(StepStatus status) => status switch
    {
        StepStatus.Passed => "passed",
        StepStatus.Failed => "failed",
        StepStatus.Broken => "broken",
        _ => "unknown",
    };

    private static string VerdictName(Verdict verdict) => verdict switch
    {
        Verdict.Unchecked => "unchecked",
        Verdict.Unchanged => "unchanged",
        Verdict.LooksRight => "looks-right",
        Verdict.LooksOff => "looks-off",
        Verdict.Sloppy => "sloppy",
        _ => "unchecked",
    };
}
