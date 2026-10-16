using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Progress = Aw.Sindri.Ui.Api.Workflow.StateGlyph;
using StageCell = Aw.Sindri.Ui.ViewModels.Workflows.StageCellViewModel;

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
    internal const string DoneToggle = "Show finished workflows";

    /// <summary>The recipe filter's name.</summary>
    internal const string RecipeFilter = "Recipe";

    /// <summary>The subject filter's name.</summary>
    internal const string SubjectFilter = "Subject";

    /// <summary>The recipe filter's "all recipes" entry.</summary>
    internal const string AllRecipes = "All recipes";

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

    /// <summary>The legend's word for the loop mark.</summary>
    internal const string LegendLoop = "loop";

    /// <summary>The legend's word for the mark of an escalated or parked stage.</summary>
    internal const string LegendEscalated = "escalated or parked";

    /// <summary>The legend's word for the mark of a taken-over stage.</summary>
    internal const string LegendTakenOver = "taken over";

    /// <summary>The legend line (screens §4.2): each mark the rows draw with its word, then the page's keys. The marks come from where the rows get them.</summary>
    internal static readonly string WorkflowsLegend =
        $"{StageCell.Mark(Progress.Done)} {StageDone}   {StageCell.Mark(Progress.Active)} {StageActive}   "
        + $"{StageCell.Mark(Progress.Pending)} {StagePending}   {MarkEscalated} {LegendEscalated}   {MarkTakenOver} {LegendTakenOver}   {LoopMark} {LegendLoop}"
        + "        Enter opens · A answer · B hand back · X cancel";

    /// <summary>The legend in words: no glyph, because a screen reader gets the words.</summary>
    internal static readonly string WorkflowsLegendName =
        $"{StageDone}, {StageActive}, {StagePending}, {LegendEscalated}, {LegendTakenOver}, {LegendLoop}. Keys: Enter opens, A answer, B hand back, X cancel";

    /// <summary>The mark of an escalated or parked stage.</summary>
    internal const string MarkEscalated = "▲";

    /// <summary>The mark of a taken-over stage.</summary>
    internal const string MarkTakenOver = "▌";

    /// <summary>B on a row: handing an instance back is sent from the app in U8b.</summary>
    internal static readonly string HandBackArrives = $"Hand back {Arrives("U8b")}. Copy its command from the instance and run it at a terminal.";

    /// <summary>N: starting a workflow from the app arrives with U5'.</summary>
    internal static readonly string StartArrives = $"Start {Arrives("U5'")}.";

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
