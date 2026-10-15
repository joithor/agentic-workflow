namespace Aw.Sindri.Ui.Platform;

/// <summary>The tray icon's states in U0 (UI spec §5's table; the needs-you states arrive with U3).</summary>
public enum TrayState
{
    /// <summary>A monochrome mark: Sindri runs and nothing needs the user.</summary>
    Neutral,

    /// <summary>A hollow mark, struck through: Sindri isn't running.</summary>
    NotRunning,

    /// <summary>A hollow mark: Sindri runs but hasn't answered for 30 s.</summary>
    Stale,
}
