namespace Aw.Sindri.Core.Sessions;

/// <summary>The rules that turn a pane capture into preview lines (collector spec CA-9, CI-10), and the liveness check. Pure.</summary>
public static class PreviewRules
{
    /// <summary>The most lines a preview shows.</summary>
    public const int MaxLines = 40;

    /// <summary>The longest line, in UTF-16 code units.</summary>
    public const int MaxLineLength = 200;

    /// <summary>The most stdout bytes a capture keeps.</summary>
    public const int MaxBytes = 64 * 1024;

    /// <summary>The most parent hops the liveness check walks.</summary>
    public const int MaxHops = 64;

    /// <summary>Steps 1 to 6 on the bytes tmux wrote.</summary>
    /// <param name="capture">The last <see cref="MaxBytes"/> plus one bytes (or fewer) of tmux's output.</param>
    /// <returns>The lines and whether anything was cut.</returns>
    public static PreviewText Of(ReadOnlySpan<byte> capture) => throw new NotSupportedException();

    /// <summary>Steps 2 to 6 on decoded text.</summary>
    /// <param name="text">The decoded capture.</param>
    /// <param name="dropped">True when the 64 KiB cap dropped output before it.</param>
    /// <returns>The lines and whether anything was cut.</returns>
    public static PreviewText Of(string text, bool dropped) => throw new NotSupportedException();

    /// <summary>True when the session's process is <paramref name="panePid"/> or has it as an ancestor within <see cref="MaxHops"/> hops.</summary>
    /// <param name="pid">The session's process id.</param>
    /// <param name="startTicks">Its start ticks: the table's process at that pid must have them.</param>
    /// <param name="panePid">The pane's first process.</param>
    /// <param name="table">The process table.</param>
    /// <returns>True when live.</returns>
    public static bool InPane(int pid, long startTicks, int panePid, IReadOnlyList<ProcessEntry> table) => throw new NotSupportedException();
}
