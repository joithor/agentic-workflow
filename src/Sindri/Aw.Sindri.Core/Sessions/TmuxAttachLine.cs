namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// The attach command for a tmux session as data (collector spec §6.2, ARCH-SESS-001): the copy line the human runs and the argv a viewer's terminal runs.
/// No Sindri type executes it.
/// </summary>
public static class TmuxAttachLine
{
    /// <summary>The argv: tmux -S &lt;socket&gt; attach -t '=&lt;session&gt;' (the '=' makes the name an exact match).</summary>
    /// <param name="socket">The server's socket path.</param>
    /// <param name="session">The session name.</param>
    /// <returns>The argv, program first.</returns>
    public static IReadOnlyList<string> Argv(string socket, string session)
    {
        ArgumentException.ThrowIfNullOrEmpty(socket);
        ArgumentException.ThrowIfNullOrEmpty(session);
        return ["tmux", "-S", socket, "attach", "-t", "=" + session];
    }

    /// <summary>The line as one string with each part shell-quoted when it needs it.</summary>
    /// <param name="socket">The server's socket path.</param>
    /// <param name="session">The session name.</param>
    /// <returns>The text to copy.</returns>
    public static string Line(string socket, string session) => string.Join(' ', Argv(socket, session).Select(Quote));

    private static string Quote(string part) =>
        part.Length > 0 && part.All(static c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-' or ':' or '%' or '@') ? part : "'" + part.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
