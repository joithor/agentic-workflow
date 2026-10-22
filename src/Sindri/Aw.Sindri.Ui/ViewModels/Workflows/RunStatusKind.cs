namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The kinds of run status the Workflows page draws (F3 workflows spec §4).</summary>
internal enum RunStatusKind
{
    /// <summary>A word this build does not know, shown as served.</summary>
    Unknown,

    /// <summary>The run is working.</summary>
    Running,

    /// <summary>A choice or decision waits on the user.</summary>
    WaitingForYou,

    /// <summary>The run stopped itself and needs the user.</summary>
    Paused,

    /// <summary>The user took the run over.</summary>
    TookOver,

    /// <summary>The run has not started.</summary>
    NotStarted,

    /// <summary>The run finished.</summary>
    Done,

    /// <summary>The run failed.</summary>
    Failed,

    /// <summary>The run ended without resolving what it was asked.</summary>
    DidntWorkOut,

    /// <summary>The run was cancelled.</summary>
    Cancelled,
}
