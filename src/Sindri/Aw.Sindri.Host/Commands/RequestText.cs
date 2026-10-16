using System.Globalization;
using System.Text;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.ListRequests;

namespace Aw.Sindri.Host.Commands;

/// <summary>The plain-text form of the request list and the needs-you list (no colour: state is always a word).</summary>
internal static class RequestText
{
    /// <summary>One block per request: its line, then its text indented; the list's sentence first when it has one.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The text.</returns>
    internal static string Requests(ListRequestsResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);   // on stale views the rows may be partial, and this says why
        }

        foreach (var request in list.Requests)
        {
            var repo = request.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            var item = request.Item is { } i ? $"  {i.Value}" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{request.Id.Value}  {RequestVerbArguments.KindName(request.Kind)}  {RequestVerbArguments.StateName(request.State)}{repo}{item}  by {request.Actor}  since {request.FiledAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}  decision {request.DecisionId.Value}"))
                .Append("  ").AppendLine(request.Text);
        }

        return text.ToString();
    }

    /// <summary>One block per row: label, title and since, then why, the notice when there is one, and the command that does it; the sentence when there is none.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The text.</returns>
    internal static string NeedsYou(GetNeedsYouResponse list)
    {
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var row in list.Rows)
        {
            var repo = row.Repo is { } r ? $"  [{r.Value}]" : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{row.Label}  {row.Title}{repo}  since {row.Since.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}"))
                .Append("  ").AppendLine(row.Reason);
            if (row.Notice is { } notice)
            {
                _ = text.Append("  ").AppendLine(notice);
            }

            _ = text.Append("  ").AppendLine(row.Cli);
        }

        return text.ToString();
    }
}
