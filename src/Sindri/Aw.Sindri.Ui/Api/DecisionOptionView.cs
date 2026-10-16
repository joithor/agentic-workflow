namespace Aw.Sindri.Ui.Api;

/// <summary>One answer a decision offers.</summary>
/// <param name="Id">The choice the app sends.</param>
/// <param name="Label">The served button label.</param>
/// <param name="RejectLike">True when it declines (the app tints it so, and its key is X).</param>
/// <param name="Cli">The command that answers the same.</param>
internal sealed record DecisionOptionView(string Id, string Label, bool RejectLike, string Cli);
