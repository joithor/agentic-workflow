namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the catalog says of Decide, and what a disabled option owes the human: why it takes no input (UI spec §7).</summary>
/// <param name="Readiness">Whether the app can run Decide now.</param>
/// <param name="Stage">For a pending Decide: the phase that brings it ("P4"); empty otherwise.</param>
/// <param name="Cli">The served command.</param>
internal readonly record struct DecideState(ActionReadiness Readiness, string Stage, string Cli)
{
    /// <summary>The tooltip and help text of a disabled option: the served command of a pending action, the protected sentence of a cli-only one; empty when Decide runs.</summary>
    internal string Reason => Readiness switch
    {
        ActionReadiness.Pending => Cli,
        ActionReadiness.CliOnly => Labels.ProtectedTooltip,
        ActionReadiness.Ready => "",
        _ => "",
    };

    /// <summary>The line shown once under the decision: the phase and the command of a pending action, the protected sentence of a cli-only one; empty when Decide runs.</summary>
    internal string Line => Readiness switch
    {
        ActionReadiness.Pending => Cli.Length > 0 ? Labels.Arrives(Stage) + " · " + Cli : Labels.Arrives(Stage),
        ActionReadiness.CliOnly => Labels.ProtectedTooltip,
        ActionReadiness.Ready => "",
        _ => "",
    };
}
