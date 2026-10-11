namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The window's state before any view's own state (UI spec §4.1's pre-API rows, highest priority first). Stalled,
/// Rebuilding, Error and Empty are per view and arrive with U1's asOf.
/// </summary>
internal enum ShellState
{
    /// <summary>The status reports an apiVersion other than <see cref="Api.SindriApiClient.AppApiVersion"/>: a full-window panel, no data, no actions.</summary>
    VersionMismatch,

    /// <summary>No usable control.json, or ping fails (SND-UI-001/002): S1b's panel; the app keeps looking every 2 s.</summary>
    NotRunning,

    /// <summary>First load, before /status answers: the chrome and skeleton rows (first load only, never on refresh).</summary>
    Connecting,

    /// <summary>SND-UI-003, the API answered with something this build doesn't read: a banner with the code; the last good data stays, marked stale.</summary>
    Unreadable,

    /// <summary>No event or ping for 30 s: "updated Ns ago" becomes the word stale.</summary>
    Stale,

    /// <summary>Connected and fresh.</summary>
    Normal,
}
