using System.Collections.Frozen;
using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Setup card on Home and the Setup page (setup-once spec §5.7). Plain words: a step is a step, a recipe is a workflow, a code waits in Details.</summary>
internal static partial class Labels
{
    // A step's action by its catalog id: the words before it is done, and the words once it is ("another", "again").
    private static readonly FrozenDictionary<string, (string First, string Again)> _setupActions = new Dictionary<string, (string First, string Again)>(StringComparer.Ordinal)
    {
        ["AddRepo"] = ("Add a repo", "Add another repo"),
        ["ApproveProfile"] = ("Review and approve", "Review and approve"),
        ["ApproveAllRecipes"] = ("Approve all workflows", "Approve again"),
        ["RegisterMcp"] = ("Connect your agents", "Connect again"),
        ["SetupOnyx"] = ("Set up Onyx", "Set up again"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The page's name (it is not a tab: the card's link, G U and the palette open it).</summary>
    internal const string SetupPage = "Setup";

    /// <summary>The page's one-line purpose.</summary>
    internal const string SetupPurpose = "Get Sindri ready to work on your repos. Each step says what it needs from you.";

    /// <summary>The card's heading.</summary>
    internal const string SetupCardHeading = "Set up Sindri";

    /// <summary>The card's link to the page.</summary>
    internal const string SetupOpen = "Open setup";

    /// <summary>The link's accessible name: its visible words first.</summary>
    internal const string SetupOpenName = "Open setup, all the steps";

    /// <summary>The card and page before the first answer.</summary>
    internal const string ReadingSetup = "Reading setup…";

    /// <summary>When Sindri has no setup list (an older Sindri).</summary>
    internal const string SetupNotServed = "This Sindri has no setup list yet.";

    /// <summary>When the answer can't be read.</summary>
    internal const string SetupUnreadable = "The setup list can't be read from this Sindri.";

    /// <summary>When the first read failed: the reason follows.</summary>
    internal static string SetupFailed(Error error) => "Couldn't read setup just now. " + InstanceRefused(error);

    /// <summary>When Sindri isn't running (the card has no Start button: the app never starts it from inside the window).</summary>
    internal const string SetupNotRunning = "Sindri isn't running.";

    /// <summary>The line over the launcher command.</summary>
    internal const string SetupStartIt = "Start it from a terminal:";

    /// <summary>What starting it does for the app.</summary>
    internal const string SetupReconnects = "The app reconnects on its own when Sindri starts.";

    /// <summary>The launcher command the daily path uses.</summary>
    internal const string SetupLauncher = "sindri-app";

    /// <summary>The page's lead when every step is done or skipped.</summary>
    internal const string SetupFinished = "Setup is finished.";

    /// <summary>The line under it.</summary>
    internal const string SetupFinishedHint = "Every step is done or skipped.";

    /// <summary>The heading over the steps in order.</summary>
    internal const string SetupAllSteps = "All the steps";

    /// <summary>The disclosure that holds ids and codes.</summary>
    internal const string SetupDetails = "Details";

    /// <summary>The page's lead eyebrow for a step that needs the person.</summary>
    internal const string SetupLeadYou = "Your next step";

    /// <summary>The lead for a step that stopped.</summary>
    internal const string SetupLeadFailed = "This stopped";

    /// <summary>The lead for a step Sindri is working on.</summary>
    internal const string SetupLeadRunning = "Sindri is working on this";

    /// <summary>The lead for a step that has not started.</summary>
    internal const string SetupLeadTodo = "Next";

    /// <summary>The hint over a copyable command.</summary>
    internal const string SetupCliHint = "Or run this in a terminal:";

    /// <summary>The Details label of a step's id.</summary>
    internal const string SetupStepField = "Step";

    /// <summary>The Details label of a step's state as served.</summary>
    internal const string SetupStateField = "State";

    /// <summary>The Details label of an error code.</summary>
    internal const string SetupCodeField = "Error code";

    /// <summary>The Details label of the action id.</summary>
    internal const string SetupActionField = "Action";

    /// <summary>The expand control's accessible name when the card shows only part: its visible words first, then what it does.</summary>
    internal static string SetupMoreName(string visible) => visible + ", show all steps";

    /// <summary>The same control once expanded.</summary>
    internal const string SetupShowFewer = "Show fewer";

    /// <summary>"2 of 6 done".</summary>
    internal static string SetupHead(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done} of {total} done");

    /// <summary>"and 3 more", and how many of those are waiting for the person.</summary>
    internal static string SetupMore(int hidden, int needYou) => needYou switch
    {
        0 => string.Create(CultureInfo.InvariantCulture, $"and {hidden} more"),
        1 => string.Create(CultureInfo.InvariantCulture, $"and {hidden} more, 1 needs you"),
        _ => string.Create(CultureInfo.InvariantCulture, $"and {hidden} more, {needYou} need you"),
    };

    /// <summary>"40 of 100" for a counted step.</summary>
    internal static string SetupProgress(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done} of {total}");

    /// <summary>A step's state as the glyph and the word shown beside it. A word this build doesn't know renders as itself.</summary>
    /// <param name="state">The state as served.</param>
    /// <param name="waiting">True for a "todo" step with no action: it waits on another step.</param>
    /// <returns>The glyph and the word.</returns>
    internal static (string Glyph, string Word) SetupChip(string state, bool waiting) => state switch
    {
        "needs-you" => ("◆", "YOUR TURN"),
        "failed" => ("✕", "FAILED"),
        "running" => ("●", "WORKING"),
        "done" => ("✓", "DONE"),
        "skipped" => ("○", "SKIPPED"),
        "todo" => ("○", waiting ? "WAITING" : "NOT STARTED"),
        _ => ("○", state.ToUpperInvariant()),
    };

    /// <summary>What the live region says when a step changes: "Approve the recipes: done".</summary>
    internal static string SetupChanged(string title, string word) => title + ": " + word.ToLowerInvariant();

    /// <summary>A step's accessible name: its title, its state and why.</summary>
    internal static string SetupStepName(string title, string word, string why) => title + ", " + word.ToLowerInvariant() + ". " + why;

    /// <summary>The words of a step's action. A step that is already done offers it again, or another.</summary>
    /// <param name="action">The catalog action id.</param>
    /// <param name="done">True when the step is done.</param>
    /// <returns>The words.</returns>
    internal static string SetupActionLabel(string action, bool done) =>
        _setupActions.TryGetValue(action, out var words) ? (done ? words.Again : words.First) : Action(action);

    /// <summary>The terminal form of a step's action when the catalog serves none (setup-once spec §7.4).</summary>
    /// <param name="action">The catalog action id.</param>
    /// <returns>The command, or empty for an action this build doesn't know.</returns>
    internal static string SetupCli(string action) => action switch
    {
        "AddRepo" => "sindri-app repo add <path>",
        "ApproveProfile" => "sindri-app profile approve <hash>",
        "ApproveAllRecipes" => "sindri-app recipe approve-all",
        "RegisterMcp" => "sindri-app mcp register --apply",
        "SetupOnyx" => "sindri-app onyx up",
        _ => string.Empty,
    };
}
