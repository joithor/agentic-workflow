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

/// <summary>One callout of the run drawer.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Class">The style class that colours it (the status badge's).</param>
/// <param name="Icon">The Icons.axaml name of its glyph.</param>
/// <param name="Title">The title line.</param>
/// <param name="Body">The words under the title; empty for none.</param>
internal sealed record InspectorCallout(InspectorCalloutKind Kind, string Class, string Icon, string Title, string Body);
