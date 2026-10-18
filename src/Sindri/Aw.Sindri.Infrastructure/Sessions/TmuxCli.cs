using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The only place Sindri runs tmux (collector spec CI-3, ARCH-SESS-001): the four list/display verbs, every call with -S naming its socket and an environment
/// without TMUX and TMUX_PANE, through its own bounded Process.Start that kills the child only. A server's answer is kept for 10 s; a socket that timed out
/// is not asked again for 10 s.
/// </summary>
/// <param name="time">The clock for the timeouts, the cache and the skip window.</param>
/// <param name="executable">The tmux program (a test passes a stand-in).</param>
/// <param name="callBudget">The longest one tmux call may run.</param>
internal sealed class TmuxCli(TimeProvider time, string executable, TimeSpan callBudget) : ITmuxServers, IPaneReader
{
    internal static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    /// <summary>The longest one tmux call runs in production (collector spec CO9).</summary>
    internal static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(1);

    private const string PaneFormat = "#{pane_pid}\t#{session_name}\t#{window_index}\t#{pane_index}\t#{pane_id}";
    private const string ClientFormat = "#{client_pid}\t#{client_tty}\t#{client_session}";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, (DateTimeOffset At, Result<TmuxServerView> View)> _answers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _timedOut = new(StringComparer.Ordinal);

    public Result<TmuxServerView> Read(string socket, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(socket);
        if (!Path.IsPathRooted(socket))
        {
            return Failed();   // CI-5: -S names an absolute socket path; a relative or abstract name is never passed on
        }

        var now = time.GetUtcNow();
        lock (_gate)
        {
            if (_timedOut.TryGetValue(socket, out var since) && now - since < Window)
            {
                return Failed();
            }

            if (_answers.TryGetValue(socket, out var kept) && now - kept.At < Window)
            {
                return kept.View;
            }
        }

        var view = Ask(socket, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return view;   // the caller gave up: not an answer to keep
        }

        lock (_gate)
        {
            _answers[socket] = (time.GetUtcNow(), view);
        }

        return view;
    }

    public PaneRead Read(string socket, string pane, CancellationToken cancellationToken) => throw new NotSupportedException();

    private Result<TmuxServerView> Ask(string socket, CancellationToken cancellationToken)
    {
        var panes = Run(socket, TmuxVerb.ListPanes, ["-a", "-F", PaneFormat], cancellationToken);
        if (panes is null)
        {
            return Failed();
        }

        var clients = Run(socket, TmuxVerb.ListClients, ["-F", ClientFormat], cancellationToken);
        return clients is null ? Failed() : new TmuxServerView(Lines(panes, Pane), Lines(clients, Client));
    }

    private static Error Failed() => SindriErrors.SessionSourceFailed.With("tmux");

    private static string Name(TmuxVerb verb) => verb switch
    {
        TmuxVerb.ListSessions => "list-sessions",
        TmuxVerb.ListPanes => "list-panes",
        TmuxVerb.ListClients => "list-clients",
        TmuxVerb.DisplayMessage => "display-message",
        TmuxVerb.CapturePane => "capture-pane",
        _ => throw new ArgumentOutOfRangeException(nameof(verb)),
    };

    /// <summary>Runs one verb on <paramref name="socket"/>; null when tmux is missing, the server doesn't answer, or the call ran past its budget (then the socket is skipped for 10 s).</summary>
    internal string? Run(string socket, TmuxVerb verb, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var part in (string[])["-S", socket, Name(verb), .. arguments])
        {
            start.ArgumentList.Add(part);
        }

        _ = start.Environment.Remove("TMUX");
        _ = start.Environment.Remove("TMUX_PANE");
        using var own = new CancellationTokenSource(callBudget, time);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, own.Token);
        Process? child;
        try
        {
            child = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // no tmux on PATH
        }

        using (child)
        {
            return child is null ? null : Wait(child, socket, limit, own);
        }
    }

    private string? Wait(Process child, string socket, CancellationTokenSource limit, CancellationTokenSource own)
    {
        // why: Register hands back the object passed in, which is this Process
        using var kill = limit.Token.Register(static state => Kill((Process)state!), child);
        child.ErrorDataReceived += static (_, _) => { };   // stderr is drained so a full pipe can't stall the child; its text isn't used
        child.BeginErrorReadLine();
        var output = child.StandardOutput.ReadToEnd();
        child.WaitForExit();
        if (limit.IsCancellationRequested)
        {
            if (own.IsCancellationRequested)
            {
                lock (_gate)
                {
                    _timedOut[socket] = time.GetUtcNow();   // only this call's own budget marks the socket; a caller's cancel is a plain failure
                }
            }

            return null;
        }

        return child.ExitCode == 0 ? output : null;
    }

    private static void Kill(Process child)
    {
        try
        {
            child.Kill();   // the child only: never its tree, never another tmux
        }
        catch (InvalidOperationException)
        {
            // it exited between the deadline and the kill
        }
    }

    private static List<T> Lines<T>(string text, Func<string[], T?> parse)
        where T : class =>
        [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => l.Split('\t')).Select(parse).OfType<T>()];

    private static TmuxPane? Pane(string[] f) =>
        f.Length == 5 && Number(f[0]) is { } pid && Number(f[2]) is { } window && Number(f[3]) is { } index ? new TmuxPane(pid, f[1], window, index, f[4]) : null;

    private static TmuxClient? Client(string[] f) => f.Length == 3 && Number(f[0]) is { } pid ? new TmuxClient(pid, f[1], f[2]) : null;

    private static int? Number(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
}
