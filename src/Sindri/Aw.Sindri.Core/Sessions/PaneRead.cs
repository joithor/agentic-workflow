namespace Aw.Sindri.Core.Sessions;

/// <summary>What one read of a pane gave (collector spec CA-9): its first process and the last bytes of its text, or why there is none.</summary>
public sealed class PaneRead
{
    private PaneRead(PaneReadKind kind, int panePid, byte[] capture)
    {
        Kind = kind;
        PanePid = panePid;
        Capture = capture;
    }

    /// <summary>The pane is not there.</summary>
    public static PaneRead Gone { get; } = new(PaneReadKind.Gone, 0, []);

    /// <summary>The deadline ran out or the preview mark is set.</summary>
    public static PaneRead TimedOut { get; } = new(PaneReadKind.TimedOut, 0, []);

    /// <summary>How the read ended.</summary>
    public PaneReadKind Kind { get; }

    /// <summary>The pane's first process (tmux's pane_pid); 0 unless captured.</summary>
    public int PanePid { get; }

    /// <summary>The last <see cref="PreviewRules.MaxBytes"/> plus one bytes tmux wrote (the extra byte tells whether the cap dropped output); empty unless captured.</summary>
    public ReadOnlyMemory<byte> Capture { get; }

    /// <summary>A pane whose text was read.</summary>
    /// <param name="panePid">The pane's first process.</param>
    /// <param name="capture">The tail of tmux's output.</param>
    /// <returns>The read.</returns>
    public static PaneRead Captured(int panePid, byte[] capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return new PaneRead(PaneReadKind.Captured, panePid, capture);
    }
}
