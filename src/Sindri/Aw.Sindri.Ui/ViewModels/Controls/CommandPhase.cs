namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>A command control's lifecycle (UI spec §4.1), the same everywhere.</summary>
internal enum CommandPhase
{
    /// <summary>Nothing in flight.</summary>
    Idle,

    /// <summary>Sent; disabled with a spinner, its label kept.</summary>
    Sending,

    /// <summary>The API accepted it: an inline check and the outcome for 2 s.</summary>
    Accepted,

    /// <summary>Refused or failed: the API's title and detail inline, until the next run.</summary>
    Failed,
}
