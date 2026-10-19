using Aw.Context.Core.History;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetHistory;

/// <summary>Cleans every piece of text a history view carries the way every answer's is cleaned (spec §7): scrubbed, one line for a label, fenced when the caller's policy fences untrusted text. A commit subject and a branch name are untrusted.</summary>
internal static class HistoryTexts
{
    internal static HistoryView Clean(HistoryView view, MapText text) => view with
    {
        Anchors = [.. view.Anchors.Select(a => a with { Label = text.Label(a.Label, a.Trust) })],
        MissingAnchors = [.. view.MissingAnchors.Select(text.Line)],
        Records = [.. view.Records.Select(r => r with { Label = text.Label(r.Label, r.Trust) })],
        Decisions = [.. view.Decisions.Select(d => d with
        {
            Text = text.Body(d.Text, d.Trust),
            Quote = d.Quote is { } q ? q with { Text = text.Body(q.Text, q.Trust) } : null,
        })],
        Changes = [.. view.Changes.Select(c => c with
        {
            Subject = text.Label(c.Subject, c.Trust),
            Branch = c.Branch is { } branch ? text.Label(branch, c.Trust) : null,
            Files = [.. c.Files.Select(text.Line)],
        })],
        Degraded = [.. view.Degraded.Select(text.Line)],
    };
}
