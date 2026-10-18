namespace Aw.Sindri.Core.Sessions;

/// <summary>The reasons a preview is unavailable (collector spec CA-9), as the response spells them.</summary>
public static class PreviewReason
{
    /// <summary>The session isn't in a tmux pane.</summary>
    public const string NotInTmux = "not-in-tmux";

    /// <summary>The pane is gone, or the session's process isn't in it.</summary>
    public const string NotLive = "not-live";

    /// <summary>The 1 s read ran out, or a recent read on the socket did.</summary>
    public const string TimedOut = "timed-out";
}
