using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The only place Sindri runs tmux (collector spec CI-3, ARCH-SESS-001): the five read verbs (list-sessions, list-panes, list-clients, display-message and, for the live preview, capture-pane), every call with -S naming its socket and an environment
/// without TMUX and TMUX_PANE, through its own bounded Process.Start that kills the child only. A server's answer is kept for 10 s; a socket that timed out
/// is not asked again for 10 s. The preview keeps its own mark per socket for the same 10 s, which the pass never reads.
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
    private const string PanePidFormat = "#{pane_pid}";
    private const string ClientFormat = "#{client_pid}\t#{client_tty}\t#{client_session}";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, (DateTimeOffset At, Result<TmuxServerView> View)> _answers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _timedOut = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _previewTimedOut = new(StringComparer.Ordinal);

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

    public PaneRead Read(string socket, string pane, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(socket);
        ArgumentException.ThrowIfNullOrEmpty(pane);
        if (!Path.IsPathRooted(socket) || !IsPaneId(pane))
        {
            return PaneRead.Gone;   // CI-5: -S names an absolute socket path and -t a '%n' pane id; nothing else is passed on
        }

        lock (_gate)
        {
            if (_previewTimedOut.TryGetValue(socket, out var since) && time.GetUtcNow() - since < Window)
            {
                return PaneRead.TimedOut;   // the preview's own mark: the pass doesn't read it
            }
        }

        using var own = new CancellationTokenSource(callBudget, time);   // one deadline for both calls: the worst case is the budget, not twice it
        var first = Spawn(new TmuxCall(socket, TmuxVerb.DisplayMessage, ["-p", "-t", pane, PanePidFormat], Keep: 64), own, cancellationToken);
        var panePid = first.Output is { } shown ? Number(Encoding.UTF8.GetString(shown).Trim()) : null;
        if (panePid is null)
        {
            return Ended(socket, first, own);
        }

        var second = Spawn(new TmuxCall(socket, TmuxVerb.CapturePane, ["-p", "-J", "-S", "-40", "-t", pane], Keep: PreviewRules.MaxBytes + 1), own, cancellationToken);
        return second.Output is { } capture ? PaneRead.Captured(panePid.Value, capture) : Ended(socket, second, own);
    }

    /// <summary>How a read ends when a call gave nothing: the deadline (which sets the preview's mark) or a pane that is not there.</summary>
    private PaneRead Ended(string socket, Spawned call, CancellationTokenSource own)
    {
        if (!call.TimedOut || !own.IsCancellationRequested)
        {
            return PaneRead.Gone;
        }

        lock (_gate)
        {
            _previewTimedOut[socket] = time.GetUtcNow();
        }

        return PaneRead.TimedOut;
    }

    private static bool IsPaneId(string pane) => pane.Length > 1 && pane[0] == '%' && pane.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

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

    /// <summary>One tmux call's outcome: its stdout (the last bytes of it), or null when tmux is missing, the server doesn't answer or the call was cut off.</summary>
    /// <param name="Output">The last bytes of stdout, or null.</param>
    /// <param name="TimedOut">True when the call was killed for running past its deadline (not for a caller's cancel).</param>
    private readonly record struct Spawned(byte[]? Output, bool TimedOut);

    /// <summary>One tmux call to make.</summary>
    /// <param name="Socket">The server's socket path.</param>
    /// <param name="Verb">One of the five read verbs.</param>
    /// <param name="Arguments">The verb's arguments.</param>
    /// <param name="Keep">How many of the last stdout bytes to keep.</param>
    private readonly record struct TmuxCall(string Socket, TmuxVerb Verb, string[] Arguments, int Keep);

    /// <summary>Runs one verb on <paramref name="socket"/>; null when tmux is missing, the server doesn't answer, or the call ran past its budget (then the socket is skipped for 10 s).</summary>
    internal string? Run(string socket, TmuxVerb verb, string[] arguments, CancellationToken cancellationToken)
    {
        using var own = new CancellationTokenSource(callBudget, time);
        var call = Spawn(new TmuxCall(socket, verb, arguments, Keep: int.MaxValue), own, cancellationToken);
        if (call.TimedOut)
        {
            lock (_gate)
            {
                _timedOut[socket] = time.GetUtcNow();   // only this call's own budget marks the socket; a caller's cancel is a plain failure
            }
        }

        return call.Output is null ? null : Encoding.UTF8.GetString(call.Output);
    }

    private Spawned Spawn(TmuxCall call, CancellationTokenSource own, CancellationToken cancellationToken)
    {
        var start = ChildEnvironment.Start(executable);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var part in (string[])["-S", call.Socket, Name(call.Verb), .. call.Arguments])
        {
            start.ArgumentList.Add(part);
        }

        _ = start.Environment.Remove("TMUX");
        _ = start.Environment.Remove("TMUX_PANE");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, own.Token);
        Process? child;
        try
        {
            child = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return new Spawned(Output: null, TimedOut: false);   // no tmux on PATH
        }

        using (child)
        {
            return child is null ? new Spawned(Output: null, TimedOut: false) : Wait(child, limit, own, call.Keep);
        }
    }

    private static Spawned Wait(Process child, CancellationTokenSource limit, CancellationTokenSource own, int keep)
    {
        // why: Register hands back the object passed in, which is this Process
        using var kill = limit.Token.Register(static state => Kill((Process)state!), child);
        child.ErrorDataReceived += static (_, _) => { };   // stderr is drained so a full pipe can't stall the child; its text isn't used
        child.BeginErrorReadLine();
        var output = ReadTail(child.StandardOutput.BaseStream, keep);
        child.WaitForExit();
        if (limit.IsCancellationRequested)
        {
            return new Spawned(Output: null, TimedOut: own.IsCancellationRequested);
        }

        return new Spawned(child.ExitCode == 0 ? output : null, TimedOut: false);
    }

    /// <summary>All of <paramref name="stream"/>, or its last <paramref name="keep"/> bytes: never more than twice that in memory.</summary>
    private static byte[] ReadTail(Stream stream, int keep)
    {
        var chunk = new byte[8192];
        using var all = new MemoryStream();
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            all.Write(chunk, 0, read);
            if (all.Length > 2L * keep)
            {
                Trim(all, keep);
            }
        }

        Trim(all, keep);
        return all.ToArray();
    }

    private static void Trim(MemoryStream all, int keep)
    {
        if (all.Length <= keep)
        {
            return;
        }

        var tail = all.GetBuffer().AsSpan((int)(all.Length - keep), keep).ToArray();
        all.SetLength(0);
        all.Write(tail, 0, tail.Length);
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
