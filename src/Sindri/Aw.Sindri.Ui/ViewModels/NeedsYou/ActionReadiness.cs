namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the catalog says of an action.</summary>
internal enum ActionReadiness
{
    /// <summary>The app runs it.</summary>
    Ready,

    /// <summary>Not available yet: shown disabled with its command.</summary>
    Pending,

    /// <summary>Only at a terminal, behind a typed confirmation.</summary>
    CliOnly,
}
