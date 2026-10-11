using System.Globalization;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Core.Publishing;

/// <summary>Plans the Linear attachments for a parity run: one side-by-side per frame.</summary>
public static class ParityAttachments
{
    /// <summary>Every attachment title from this tool starts with this, so earlier ones can be found.</summary>
    public const string TitlePrefix = "Pixel diff: ";

    /// <summary>Linear's subtitle limit.</summary>
    public const int SubtitleMax = 500;

    /// <summary>The attachment title for a frame.</summary>
    /// <param name="frame">The frame's name.</param>
    /// <returns>The title.</returns>
    public static string Title(string frame) => $"{TitlePrefix}{frame} (Figma | implementation | diff)";

    /// <summary>The subtitle: design node (the frame's own first), build, viewport, diff and known differences; cut to 500 characters with an ellipsis.</summary>
    /// <param name="summary">The parity run.</param>
    /// <param name="frame">The frame.</param>
    /// <returns>The subtitle.</returns>
    public static string Subtitle(ParitySummary summary, ParityFrame frame)
    {
        var known = frame.KnownDifferences.Count > 0 ? $"known differences: {string.Join("; ", frame.KnownDifferences)}" : "no known differences";
        var text = string.Join(" · ",
            $"design node {frame.DesignNode ?? summary.DesignNode ?? "unspecified"}",
            $"build {summary.AppBuild ?? "unverified"}",
            $"viewport {summary.Viewport.Describe()}",
            string.Create(CultureInfo.InvariantCulture, $"diff {frame.DiffPercent}% ({frame.DiffPixels}/{frame.Total} px)"),
            known);
        return text.Length > SubtitleMax ? string.Concat(text.AsSpan(0, SubtitleMax - 1), "…") : text;
    }

    /// <summary>The plan: one attachment per frame, in frame order.</summary>
    /// <param name="summary">The parity run.</param>
    /// <param name="sizeOf">The size of each side-by-side file.</param>
    /// <returns>The planned attachments.</returns>
    public static IReadOnlyList<PlannedAttachment> Plan(ParitySummary summary, Func<string, long> sizeOf) =>
        [.. summary.Frames.Select(f => new PlannedAttachment(
            f.Name,
            f.Files.SideBySide,
            RunPath.NameOf(f.Files.SideBySide),
            "image/png",
            sizeOf(f.Files.SideBySide),
            Title(f.Name),
            Subtitle(summary, f),
            string.Create(CultureInfo.InvariantCulture,
                $"{f.Name}: left = design, middle = implementation, right = diff; settled (no fade-in), no focus ring, {f.DiffPercent}% differing")))];

    /// <summary>Planned files the agent has not confirmed it opened and compared.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="reviewed">Files the agent reviewed.</param>
    /// <returns>The unreviewed files, in plan order.</returns>
    public static IReadOnlyList<string> Unreviewed(IReadOnlyList<PlannedAttachment> plan, IReadOnlyCollection<string> reviewed) =>
        [.. plan.Select(static p => p.File).Where(f => !reviewed.Contains(f, StringComparer.Ordinal))];
}
