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

    /// <summary>The badge a stage cell shows in place of its glyph.</summary>
    /// <param name="kind">Why the engine owns the state.</param>
    /// <param name="reason">The reason, as served.</param>
    /// <returns>The words; a taken-over state reads its served reason behind a mark.</returns>
    internal static string CellBadge(EngineOwnedKind kind, string reason) => kind switch
    {
        EngineOwnedKind.TakenOver => $"▌ {reason}",
        EngineOwnedKind.Escalated => EngineBadge(kind, reason),
        EngineOwnedKind.Parked => EngineBadge(kind, reason),
        _ => string.Empty,
    };
}
