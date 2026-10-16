namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One option button of the decision sheet.</summary>
/// <param name="Id">The option id.</param>
/// <param name="Label">The label as served.</param>
/// <param name="IsRecommended">True for the recommended option.</param>
/// <param name="IsEnabled">True when it takes input.</param>
internal sealed record DecisionOption(string Id, string Label, bool IsRecommended, bool IsEnabled);
