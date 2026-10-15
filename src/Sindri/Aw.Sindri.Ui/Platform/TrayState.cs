namespace Aw.Sindri.Ui.Platform;

/// <summary>The tray icon's states (UI spec §5's table).</summary>
public enum TrayState
{
    /// <summary>A monochrome mark: Sindri runs and nothing needs the user.</summary>
    Neutral,

    /// <summary>A hollow mark, struck through: Sindri isn't running.</summary>
    NotRunning,

    /// <summary>A hollow mark: Sindri runs but hasn't answered for 30 s.</summary>
    Stale,

    /// <summary>The mark in the attention colour with the count: rows need the user (a pulse or colour row exists).</summary>
    Attention,

    /// <summary>The monochrome mark with a small count: only calm rows (to look at), nothing urgent.</summary>
    Calm,
}
