namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>What Home's Needs you list is narrowed to (screens spec §3: G D).</summary>
internal enum HomeFilter
{
    /// <summary>Every row.</summary>
    All,

    /// <summary>Only the decision rows (Decisions fold into Needs you, owner answer D3).</summary>
    Decisions,
}
