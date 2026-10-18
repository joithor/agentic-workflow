namespace Aw.Sindri.Core.Sessions;

/// <summary>How a read of one pane ended (collector spec CA-9).</summary>
public enum PaneReadKind
{
    /// <summary>The pane exists and its text was captured.</summary>
    Captured,

    /// <summary>The pane is not there: the socket, the server or the pane id is gone, or tmux can't be run.</summary>
    Gone,

    /// <summary>The 1 s deadline ran out, or the socket's preview mark is set.</summary>
    TimedOut,
}
