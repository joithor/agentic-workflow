using System.Collections.Frozen;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): action labels keyed by catalog action id, and the controls' words (§7, §4.1, §12.4).</summary>
internal static partial class Labels
{
    // Labels by action id (§12.4: "labels live in one resource file keyed by action id"). An id not here shows itself.
    private static readonly FrozenDictionary<string, string> _actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ApproveProfile"] = "Approve profile",
        ["ApproveRecipe"] = "Approve recipe",
        ["ApplyCommand:answer-accept"] = "Accept",
        ["ApplyCommand:answer-grant"] = "Grant rounds",
        ["ApplyCommand:answer-grant-tokens"] = "Grant tokens",
        ["ApplyCommand:answer-reject"] = "Reject",
        ["ApplyCommand:answer-resume"] = "Resume",
        ["ApplyCommand:answer-text"] = "Answer",
        ["ApplyCommand:cancel"] = "Cancel instance",
        ["ApplyCommand:handback"] = "Hand back",
        ["ApplyCommand:takeover"] = "Take over",
        ["CancelJob"] = "Cancel job",
        ["CloseItem:cancelled"] = "Close as cancelled",
        ["CloseItem:done"] = "Close item",
        ["CompleteStep"] = "Complete",
        ["CreateItem"] = "Create item",
        ["Decide"] = "Decide",
        ["FileRequest"] = "File a request",
        ["OpenItem:attach"] = "Attach",
        ["OpenItem:open"] = "Open",
        ["OpenItem:resume"] = "Resume session",
        ["PauseService"] = "Pause service",
        ["ProbeLaunchTargets"] = "Check launch targets again",
        ["RebuildViews"] = "Rebuild views",
        ["ResumeService"] = "Resume service",
        ["RunDoctor"] = "Run doctor",
        ["RunService"] = "Run now",
        ["StartInstance"] = "Start workflow",
        ["StopProcess"] = "Stop Sindri",
        ["ValidateProfile"] = "Validate profile",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The copy button.</summary>
    internal const string Copy = "Copy";

    /// <summary>After a copy, for 1.5 s (§4.1, §12.4).</summary>
    internal const string Copied = "Copied";

    /// <summary>The copy block's line (§7, §12.4).</summary>
    internal const string TypedConfirmation = "Needs a typed confirmation at a terminal.";

    /// <summary>An action's label, keyed by its catalog id; an id the app has no label for shows itself.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The label.</returns>
    internal static string Action(string action) => _actions.TryGetValue(action, out var label) ? label : action;

    /// <summary>A pending control's phase: "arrives in P2".</summary>
    /// <param name="phase">The phase the catalog names.</param>
    /// <returns>The words.</returns>
    internal static string Arrives(string phase) => "arrives in " + phase;

    /// <summary>The accepted outcome (§4.1, §12.4: built from the action's label): "Rebuild views: done".</summary>
    /// <param name="label">The action's label.</param>
    /// <returns>The words.</returns>
    internal static string Done(string label) => label + ": done";

    /// <summary>Health's line over the pending rows (§4.8): "12 actions arrive in later phases".</summary>
    /// <param name="count">How many rows are pending.</param>
    /// <returns>The words.</returns>
    internal static string PendingSummary(int count) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "action arrives" : "actions arrive")} in later phases");

    /// <summary>The health pill's word for the status's health.word (E-14); a word this build doesn't know shows as served.</summary>
    /// <param name="word">'healthy', 'degraded', 'stalled' or 'rebuilding'.</param>
    /// <returns>The pill's word.</returns>
    internal static string HealthWord(string word) => word switch
    {
        "healthy" => "Healthy",
        "degraded" => "Degraded",
        "stalled" => "Stalled",
        "rebuilding" => "Rebuilding",
        _ => word,
    };
}
