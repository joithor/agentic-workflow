using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The one place a decision's option buttons are built, for the decision sheet and the item sheet alike: an option takes input
/// only when the catalog serves Decide as ready, the decision is not already sent, and (on the decision sheet) its row has not cleared.
/// </summary>
internal static class DecisionOptions
{
    /// <summary>The options of a pending decision as served (the item sheet's).</summary>
    /// <param name="decision">The pending decision.</param>
    /// <param name="readiness">What the catalog says of Decide.</param>
    /// <param name="sender">Knows which decisions were sent.</param>
    /// <returns>One option per served option, in the served order.</returns>
    internal static IReadOnlyList<DecisionOption> From(DecisionView decision, ActionReadiness readiness, DecisionSender sender)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(sender);
        return [.. decision.Options.Select(o => new DecisionOption(o.Id, o.Label, string.Equals(o.Id, decision.Recommended, StringComparison.Ordinal), readiness == ActionReadiness.Ready && !sender.IsSent(decision.Id), decision.Id))];
    }

    /// <summary>The options of a decision row's option controls (the decision sheet's), whose readiness the mapper already folded into each control.</summary>
    /// <param name="controls">The row's controls.</param>
    /// <param name="decisionId">The decision.</param>
    /// <param name="cleared">True once the row left the feed.</param>
    /// <param name="sender">Knows which decisions were sent.</param>
    /// <returns>One option per option control.</returns>
    internal static IReadOnlyList<DecisionOption> From(IEnumerable<NeedsYouActionControl> controls, string decisionId, bool cleared, DecisionSender sender)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(sender);
        return [.. controls.Where(c => c.Action.OptionId.Length > 0).Select(c => new DecisionOption(c.Action.OptionId, c.Action.Label, c.Action.Recommended, c.IsEnabled && !cleared && !sender.IsSent(decisionId), decisionId))];
    }
}
