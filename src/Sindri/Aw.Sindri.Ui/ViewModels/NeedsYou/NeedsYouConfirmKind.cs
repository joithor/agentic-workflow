namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the bar offers on a row whose primary is one of the three approvals the app confirms.</summary>
internal enum NeedsYouConfirmKind
{
    /// <summary>The row's primary is not one of the three.</summary>
    None,

    /// <summary>The typed box: the drifted profile.</summary>
    Typed,

    /// <summary>The hold button: approve all new workflows.</summary>
    Hold,

    /// <summary>The terminal copy: the catalog does not let the app confirm it (yet), or its typed approve comes in a later slice.</summary>
    Terminal,
}
