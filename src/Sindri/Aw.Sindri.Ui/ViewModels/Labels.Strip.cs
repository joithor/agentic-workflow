using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Home Workflows strip (screens §4.1).</summary>
internal static partial class Labels
{
    /// <summary>The strip row's desk answer button.</summary>
    internal const string Answer = "Answer";

    /// <summary>The words of a row whose answer a child instance decides: the row points at the child instead of offering an answer.</summary>
    internal const string AnswerInChild = "Answer in the child";

    /// <summary>The accessible name of the "Answer in the child" control: the drawn words lead (WCAG 2.5.3), then the instance it opens.</summary>
    /// <param name="address">The address of the instance the control opens.</param>
    /// <returns>The name.</returns>
    internal static string AnswerInChildName(string address) => $"{AnswerInChild} {address}";

    /// <summary>The accessible name of the control when no child was served: it opens the parent's frame at its Children, so it says so rather than name an address that is not the child's.</summary>
    /// <param name="parentAddress">The parked instance's address.</param>
    /// <returns>The name.</returns>
    internal static string AnswerInChildParentName(string parentAddress) => $"{AnswerInChild}, opens {parentAddress} children";

    /// <summary>The accessible name of the control on a needs-you row, which is served no child: it opens the instance the row stands for (or the row's own sheet), so it names that rather than a child.</summary>
    /// <param name="shownId">What the row shows as its id.</param>
    /// <returns>The name.</returns>
    internal static string AnswerInChildOpensName(string shownId) => $"{AnswerInChild}, opens {shownId}";

    /// <summary>The accessible name of the expanded state's control when no child was served: it moves focus to this frame's Children panel.</summary>
    internal const string AnswerInChildPanelName = AnswerInChild + ", moves to the Children panel";

    /// <summary>What the strip says while the workflow list route is not served yet (screens §4.8).</summary>
    internal const string ArrivesWorkflowStrip = "The workflow list isn't available from this Sindri yet.";

    /// <summary>What the strip says when the workflow list answers in a shape this build can't read (an older Sindri; screens §4.8, until API P11).</summary>
    internal const string StripUnreadable = "This Sindri answered in a form this app can't read yet. Updating either one usually fixes it.";

    /// <summary>The strip's overflow row: "… 3 more".</summary>
    /// <param name="count">How many open rows are not shown.</param>
    /// <returns>The words.</returns>
    internal static string MoreRows(int count) => string.Create(CultureInfo.InvariantCulture, $"… {count} more");

    /// <summary>What is said when the selected strip row finished and left.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>The words.</returns>
    internal static string StripDone(string address) => $"{address} done";

    /// <summary>The Home strip row's name: the workflow, then the item.</summary>
    /// <param name="workflow">The workflow's display name.</param>
    /// <param name="subject">The item's title.</param>
    /// <returns>The name.</returns>
    internal static string StripRowName(string workflow, string subject) => $"{subject}, {workflow}";

    /// <summary>The Home strip's answer button name: the visible word, then the item.</summary>
    /// <param name="subject">The item's title.</param>
    /// <returns>The name.</returns>
    internal static string AnswerName(string subject) => $"{Answer}, {subject}";

    /// <summary>The verb of a protected answer's lock button ("Accept", "Grant rounds").</summary>
    /// <param name="kind">How the answer is given.</param>
    /// <returns>The verb.</returns>
    internal static string AnswerVerb(AnswerKind kind) => kind switch
    {
        AnswerKind.Accept => "Accept",
        AnswerKind.Grant => "Grant rounds",
        AnswerKind.GrantTokens => "Grant tokens",
        AnswerKind.Resume => "Resume",
        AnswerKind.CancelOnly => "Cancel",
        AnswerKind.Text => Answer,
        _ => Answer,
    };

    /// <summary>The engine badge of a row that waits on the human: a triangle, the word the engine gave and its reason.</summary>
    /// <param name="kind">Why the engine owns the state.</param>
    /// <param name="reason">The reason, as served.</param>
    /// <returns>The words, or empty for a kind that has no badge (a taken-over row says WITH YOU in its chip).</returns>
    internal static string EngineBadge(EngineOwnedKind kind, string reason) => kind switch
    {
        EngineOwnedKind.Escalated => $"{MarkEscalated} escalated: {reason}",
        EngineOwnedKind.Parked => $"{MarkEscalated} parked: {reason}",
        EngineOwnedKind.TakenOver => string.Empty,
        _ => string.Empty,
    };
}
