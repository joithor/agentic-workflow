using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Workflows page (screens §4.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>A collapsed group's way back to its rows.</summary>
    internal const string ShowAll = "show all ▾";

    /// <summary>What a stage reads while a required input is missing (blocked).</summary>
    internal const string BlockedMark = "—";

    /// <summary>A present input, in words (the check is never the only sign).</summary>
    internal const string InputPresent = "present";

    /// <summary>A missing input, in words.</summary>
    internal const string InputMissing = "missing";

    /// <summary>A finished stage, in words.</summary>
    internal const string StageDone = "done";

    /// <summary>The stage the instance is in, in words.</summary>
    internal const string StageActive = "in progress";

    /// <summary>A stage not reached, in words.</summary>
    internal const string StagePending = "not started";

    /// <summary>A stage that waits on a missing required input, in words.</summary>
    internal const string StageBlocked = "blocked";

    /// <summary>The page's empty line when a filter leaves no row and the API served no sentence.</summary>
    internal const string NoWorkflowsMatch = "No workflow matches.";

    /// <summary>The finished line's expand control name.</summary>
    internal const string DoneToggle = "Show finished runs";

    /// <summary>The workflow-type filter's name.</summary>
    internal const string RecipeFilter = "Workflow type";

    /// <summary>The subject filter's name.</summary>
    internal const string SubjectFilter = "Item";

    /// <summary>The recipe filter's "all recipes" entry.</summary>
    internal const string AllRecipes = "All workflow types";

    /// <summary>The column that names what an instance is about.</summary>
    internal const string ColumnSubject = "SUBJECT";

    /// <summary>The column that lists the declared inputs.</summary>
    internal const string ColumnInputs = "INPUTS";

    /// <summary>The column that carries the served now sentence.</summary>
    internal const string ColumnNow = "NOW";

    /// <summary>A group whose rows show, in words.</summary>
    internal const string GroupExpanded = "expanded";

    /// <summary>A group collapsed to its count, in words.</summary>
    internal const string GroupCollapsed = "collapsed";

    /// <summary>The page's heading while it has no counts yet.</summary>
    internal const string WorkflowsHeading = "Workflows";

    /// <summary>The Workflows key-note live region while it says nothing (never empty, and not a word that repeats the heading).</summary>
    internal const string KeyNotesResting = "Key notes";

    /// <summary>The Cancel-note live region while it says nothing (never empty, and not a lone word that reads like a control).</summary>
    internal const string PendingNotesResting = "Cancel notes";

    /// <summary>The word a screen reader hears after the cell of the state the instance is in.</summary>
    internal const string StageCurrent = "current";

    /// <summary>The legend's word for the loop mark.</summary>
    internal const string LegendLoop = "repeats";

    /// <summary>The legend's word for the mark of an escalated or parked stage.</summary>
    internal const string LegendEscalated = "paused: needs a decision";

    /// <summary>The legend's word for the mark of a taken-over stage.</summary>
    internal const string LegendTakenOver = "you took over";

    /// <summary>The mark of a finished stage (and a produced fact).</summary>
    internal const string MarkDone = "●";

    /// <summary>The mark of the stage in progress (and a fact in progress).</summary>
    internal const string MarkActive = "◐";

    /// <summary>The mark of a stage not started (and a pending fact).</summary>
    internal const string MarkPending = "○";

    /// <summary>The mark of a declared input that is present.</summary>
    internal const string MarkInputPresent = "✓";

    /// <summary>The mark of a declared input that is missing.</summary>
    internal const string MarkInputMissing = "✕";

    /// <summary>The Workflow cell's word for a stage that waits on the human (a desk stage).</summary>
    internal const string StageDesk = "waiting for you";

    /// <summary>Who is on a run the human took over.</summary>
    internal const string WhoYouTookOver = "You took it over";

    /// <summary>Who is on a run the engine paused for a decision.</summary>
    internal const string WhoPaused = "Paused: needs a decision";

    /// <summary>Who is on a run that waits for the human.</summary>
    internal const string WhoWaitingForYou = "Waiting for you";

    /// <summary>Who is on a run that is moving on its own.</summary>
    internal const string WhoInProgress = "In progress";

    /// <summary>The up-front line of a run with inputs still missing.</summary>
    /// <param name="count">How many declared inputs are missing.</param>
    /// <returns>"1 input missing", "3 inputs missing".</returns>
    internal static string InputsMissing(int count) => count == 1 ? "1 input missing" : string.Create(CultureInfo.InvariantCulture, $"{count} inputs missing");

    /// <summary>The row's disclosure.</summary>
    internal const string RowDetails = "Details";

    /// <summary>The key line's disclosure at the foot of the page.</summary>
    internal const string KeyHeader = "Key and shortcuts";

    /// <summary>The Details line that names the stages.</summary>
    internal const string StagesHeading = "Stages";

    /// <summary>The Details line that names the inputs.</summary>
    internal const string InputsDetailsHeading = "Inputs";

    /// <summary>The Details line that names the workflow type's id.</summary>
    internal const string WorkflowTypeIdHeading = "Workflow type id";

    /// <summary>The Details line that names the run's address.</summary>
    internal const string RunAddressHeading = "Run address";

    /// <summary>The Workflows head: the root-wide counts.</summary>
    /// <param name="open">How many are open.</param>
    /// <param name="needsYou">How many need the human.</param>
    /// <param name="withYou">How many are with the human.</param>
    /// <returns>"8 open · 2 need you · 1 with you".</returns>
    internal static string WorkflowsHead(string open, string needsYou, string withYou) => $"{open} open · {needsYou} need you · {withYou} with you";

    /// <summary>A group's run count.</summary>
    /// <param name="count">How many runs.</param>
    /// <returns>"1 run", "3 runs".</returns>
    internal static string RunCount(int count) => count == 1 ? "1 run" : string.Create(CultureInfo.InvariantCulture, $"{count} runs");

    /// <summary>A row's open-the-run button name: the visible title first.</summary>
    /// <param name="subject">The item's title.</param>
    /// <param name="workflow">The workflow's display name.</param>
    /// <returns>The name.</returns>
    internal static string OpenRunName(string subject, string workflow) => $"Open {subject}, {workflow}";

    /// <summary>The mark of an escalated or parked stage.</summary>
    internal const string MarkEscalated = "▲";

    /// <summary>The mark of a taken-over stage.</summary>
    internal const string MarkTakenOver = "▌";

    /// <summary>B on a row: handing an instance back is sent from the app in U8b.</summary>
    internal const string HandBackArrives = "Handing a run back isn't available in the app yet. Copy its command from the run and run it at a terminal.";

    /// <summary>The frame tab strip's heading for screen readers.</summary>
    internal const string TabsHeading = "Instance views";

    /// <summary>The tabs' words.</summary>
    internal const string TabTimeline = "Timeline";

    /// <summary>The Decisions tab's word.</summary>
    internal const string TabDecisions = "Decisions";

    /// <summary>The Requests tab's word.</summary>
    internal const string TabRequests = "Requests";

    /// <summary>The Context tab's word.</summary>
    internal const string TabContext = "Context";

    /// <summary>The phase of the Timeline, Decisions and Requests tabs.</summary>
    internal static readonly string TabsArriveU8b = Arrives("U8b");

    /// <summary>The phase of the Context tab.</summary>
    internal static readonly string TabArrivesU9 = Arrives("U9");

    /// <summary>A tab's text: the label and, when the API serves one, the count.</summary>
    /// <param name="label">The tab's word.</param>
    /// <param name="count">The count, or null.</param>
    /// <returns>The words.</returns>
    internal static string TabText(string label, int? count) => count is { } n ? $"{label} {n.ToString(CultureInfo.InvariantCulture)}" : label;

    /// <summary>A tab's accessible name: its text and the phase that makes it live.</summary>
    /// <param name="text">The tab's text.</param>
    /// <param name="phase">The phase note.</param>
    /// <returns>The words.</returns>
    internal static string TabName(string text, string phase) => $"{text}, {phase}";

    /// <summary>The badge a stage cell shows in place of its glyph.</summary>
    /// <param name="kind">Why the engine owns the state.</param>
    /// <param name="reason">The reason, as served.</param>
    /// <returns>The words; a taken-over state reads its served reason behind a mark.</returns>
    internal static string CellBadge(EngineOwnedKind kind, string reason) => kind switch
    {
        EngineOwnedKind.TakenOver => $"{MarkTakenOver} {reason}",
        EngineOwnedKind.Escalated => EngineBadge(kind, reason),
        EngineOwnedKind.Parked => EngineBadge(kind, reason),
        _ => string.Empty,
    };
}
