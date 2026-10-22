using System.Text;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The brief's text for a tracker record (scoping spec §4): its title and description, its comments, and each child issue with its own comments. Untrusted text, which the Step scrubs.</summary>
internal static class TrackerBrief
{
    /// <summary>The text, or an empty string when the record has no description, no comment and no child (an issue with nothing to scope).</summary>
    /// <param name="record">The record.</param>
    /// <returns>The text.</returns>
    internal static string Compose(TrackerRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.Text) && record.Comments.Count == 0 && record.Children.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder().Append("# ").AppendLine(record.Title).AppendLine().AppendLine(record.Text.Trim());
        Comments(text, record.Comments, "## Comments");
        if (record.Children.Count > 0)
        {
            _ = text.AppendLine().AppendLine("## Issues");
            foreach (var child in record.Children)
            {
                _ = text.AppendLine().Append("### ").Append(child.Key).Append(": ").AppendLine(child.Title).AppendLine(child.Text.Trim());
                Comments(text, child.Comments, "#### Comments");
            }
        }

        if (record.Truncated)
        {
            _ = text.AppendLine().AppendLine("[truncated: issues or comments over the read's cap were left out]");
        }

        return text.ToString();
    }

    private static void Comments(StringBuilder text, IReadOnlyList<TrackerComment> comments, string heading)
    {
        if (comments.Count == 0)
        {
            return;
        }

        _ = text.AppendLine().AppendLine(heading);
        foreach (var comment in comments)
        {
            _ = text.Append("- ").Append(comment.Author).Append(": ").AppendLine(comment.Text.Trim());
        }
    }
}
