namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One option button of the decision sheet.</summary>
/// <param name="Id">The option id.</param>
/// <param name="Label">The label as served.</param>
/// <param name="IsRecommended">True for the recommended option.</param>
/// <param name="IsEnabled">True when it takes input.</param>
/// <param name="DecisionId">The decision the option belongs to, so its name says which one it decides.</param>
/// <param name="Reason">Why the option takes no input (the served command of a pending action, the protected sentence); empty when it can.</param>
internal sealed record DecisionOption(string Id, string Label, bool IsRecommended, bool IsEnabled, string DecisionId = "", string Reason = "")
{
    /// <summary>The accessible name: the label and the decision's id (several decisions can be on screen, each with an "Accept").</summary>
    internal string Name => DecisionId.Length > 0 ? Label + " " + DecisionId : Label;
}
