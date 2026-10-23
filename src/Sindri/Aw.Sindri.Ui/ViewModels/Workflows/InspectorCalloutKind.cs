namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The kinds of callout the run drawer shows under its header (F3 workflows spec §2 "Callout").</summary>
internal enum InspectorCalloutKind
{
    /// <summary>No callout: the run is done, not started or cancelled.</summary>
    None,

    /// <summary>A choice or decision waits on the user.</summary>
    NeedsYou,

    /// <summary>The run stopped itself and needs the user.</summary>
    Paused,

    /// <summary>The run failed or ended without resolving what it was asked.</summary>
    Failed,

    /// <summary>The user took the run over.</summary>
    TookOver,

    /// <summary>The run is working: the step it is on.</summary>
    Running,
}
