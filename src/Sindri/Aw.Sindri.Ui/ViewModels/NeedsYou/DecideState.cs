namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the catalog says of Decide, and what a disabled option owes the human: why it takes no input (UI spec §7).</summary>
/// <param name="Readiness">Whether the app can run Decide now.</param>
/// <param name="Stage">For a pending Decide: the phase that brings it ("P4"); empty otherwise.</param>
/// <param name="Cli">The served command.</param>
/// <param name="Read">False while the catalog has not been read, so Decide's absence from it means nothing yet.</param>
internal readonly record struct DecideState(ActionReadiness Readiness, string Stage, string Cli, bool Read = true)
{
    /// <summary>The tooltip and help text of a disabled option: the served command of a pending action, the protected sentence of a cli-only one; empty when Decide runs.</summary>
    internal string Reason => Readiness switch
    {
        ActionReadiness.Pending => Cli.Length > 0 ? Cli : Line,
        ActionReadiness.CliOnly => Labels.ProtectedTooltip,
        ActionReadiness.Ready => "",
        _ => "",
    };

    // Pending with no phase: the catalog is unread (say so) or lists no Decide (the served command is all there is to say).
    private string Unphased => !Read ? Labels.CheckingCatalog : Cli.Length > 0 ? Cli : Labels.DecideUnlisted;

    /// <summary>The line shown once under the decision: the phase and the command of a pending action, the protected sentence of a cli-only one; empty when Decide runs.</summary>
    internal string Line => Readiness switch
    {
        ActionReadiness.Pending => Stage.Length > 0 ? (Cli.Length > 0 ? Labels.Arrives(Stage) + " · " + Cli : Labels.Arrives(Stage)) : Unphased,
        ActionReadiness.CliOnly => Labels.ProtectedTooltip,
        ActionReadiness.Ready => "",
        _ => "",
    };
}
