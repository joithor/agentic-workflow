namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar (<see cref="NeedsYouBarViewModel"/>, continued): the outcome the step before the stop recommends (needs-you spec §4). The
/// recommendation marks one button "Suggested" and its why is the bar's resting hint; nothing recommends an outcome the buttons don't have.
/// </summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>The tag on the suggested button.</summary>
    public string SuggestedTag { get; } = Labels.NeedsYouSuggested;

    /// <summary>True when the recommended outcome is approve.</summary>
    public bool ApproveSuggested => _target?.Recommended is { Outcome: var outcome } && string.Equals(outcome, "approve", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the recommended outcome is one that sends it back (revise, reviseDesign, revisePlan).</summary>
    public bool SendBackSuggested => _target?.Recommended is { Outcome: var outcome } && outcome.StartsWith("revise", StringComparison.OrdinalIgnoreCase);

    // The recommendation's why, while one of the buttons carries the tag; else nothing.
    private string SuggestedHint => (ApproveSuggested || SendBackSuggested) && _target?.Recommended?.Why is { Length: > 0 } why ? why : "";
}
