using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>Plain text for the review verbs: words, never colour, so NO_COLOR and pipes read the same.</summary>
internal static class ContextReviewText
{
    internal static string Queue(ReviewQueueView queue) => queue.Draft;

    internal static string Entry(ReviewEntry entry, int index, int total) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{entry.Label}{index}{total}");

    internal static string History(ReviewHistoryView history) => history.Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string Result(string verb, ReviewResult result) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{verb}{result.Undecided}");

    internal static string Review(ReviewContextMapResponse response) => response.Summary ?? string.Empty;

    internal static string Exported(ReviewExported exported) => exported.Path;

    internal static string Promote(PromoteContextMapResponse response) => response.Draft;

    internal static string Proposals(ProposalsView view) => view.Run;

    internal static string Proposed(ProposeContextMapResponse response) => response.Result.Draft.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
