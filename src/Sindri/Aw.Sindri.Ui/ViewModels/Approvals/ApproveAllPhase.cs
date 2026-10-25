namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>Where the approve-all dialog is.</summary>
internal enum ApproveAllPhase
{
    /// <summary>Shut.</summary>
    Closed,

    /// <summary>The plan is being read.</summary>
    Reading,

    /// <summary>The plan is shown; the person can approve it.</summary>
    Plan,

    /// <summary>Nothing waits for approval.</summary>
    Empty,

    /// <summary>The approval runs as a job.</summary>
    Applying,

    /// <summary>The approval ended.</summary>
    Done,

    /// <summary>The plan or the approval could not be read or run; the dialog says why.</summary>
    Failed,
}
