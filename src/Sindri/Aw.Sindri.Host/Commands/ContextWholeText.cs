using System.Text;
using Aw.Context.Core.WholeMap;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>Plain text for 'context map whole': the counts line, then one line per area, biggest first, so a terminal sees the overview the page draws.</summary>
internal static class ContextWholeText
{
    internal static string Whole(WholeMapView view, bool fence)
    {
        var commit = view.MapCommit is null ? string.Empty : "@" + view.MapCommit;
        var text = new StringBuilder(ContextLensText.Invariant($"whole map {view.MapVersion}{commit}{(view.Draft ? " (draft)" : string.Empty)} · {view.Counts.Nodes} nodes, {view.Counts.Edges} edges · detail: {view.Detail.ToString().ToLowerInvariant()}{(view.Fallback is null ? string.Empty : " (" + view.Fallback + ")")}\n"));
        _ = text.Append("  ").AppendJoin(", ", view.Counts.ByKind.Where(k => k.Value > 0).Select(k => ContextLensText.Invariant($"{k.Value} {k.Key}"))).AppendLine();
        foreach (var area in view.Areas.OrderByDescending(a => a.Members).ThenBy(a => a.Key, StringComparer.Ordinal))
        {
            var kinds = string.Join(", ", area.ByKind.Select(k => ContextLensText.Invariant($"{k.Value} {k.Key}")));
            _ = text.AppendLine(ContextLensText.Invariant($"  {ContextLensText.Fenced(area.Label, area.Trust, fence)} · {area.Members} {(area.Members == 1 ? "member" : "members")} · {kinds}"));
        }

        if (view.Truncated)
        {
            _ = text.AppendLine("  more areas than the answer holds; ask for one by key");
        }

        foreach (var placed in view.Located ?? [])
        {
            _ = text.AppendLine(ContextLensText.Invariant($"  {placed.Key} → {placed.Area ?? "not in the map"}"));
        }

        return text.ToString();
    }
}
