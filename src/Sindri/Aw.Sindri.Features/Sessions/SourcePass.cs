using System.Globalization;
using System.Runtime.CompilerServices;
using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// The sources of one pass, each read on its own task under its own deadline. A source that times out or throws is one problem row and an empty answer for the rest of
/// the pass; one whose earlier read is still running is not started again (no pile-up) and is reported the same way.
/// </summary>
internal sealed class SourcePass(Dictionary<string, Task> abandoned, TimeSpan budget, List<SessionProblem> problems, TimeProvider time, CancellationToken shutdown)
{
    private const int Sources = 5;
    private static readonly TimeSpan _environmentFloor = TimeSpan.FromMilliseconds(250);
    private readonly Dictionary<string, TimeSpan> _spent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    /// <summary>Reads one source (for one process, when <paramref name="pid"/> is given) under its own deadline; abandonment is keyed by source and pid, so one stuck pid blocks only itself.</summary>
    internal (SourceOutcome Outcome, T? Value) Run<T>(string name, int? pid, Func<CancellationToken, T> read)
    {
        shutdown.ThrowIfCancellationRequested();
        var key = pid is { } p ? string.Create(CultureInfo.InvariantCulture, $"{name}:{p}") : name;
        if (_failed.Contains(key) || (abandoned.TryGetValue(key, out var running) && !running.IsCompleted))
        {
            _ = _failed.Add(key);
            return (SourceOutcome.TimedOut, default);
        }

        _ = abandoned.Remove(key);
        var share = Share(name);
        var begun = time.GetTimestamp();
        var used = _spent.GetValueOrDefault(name);
        var remaining = Min(share, budget - used);   // every pid of one source together stays inside one budget
        if (remaining <= TimeSpan.Zero)
        {
            _ = _failed.Add(key);
            return (SourceOutcome.TimedOut, default);
        }

        var (task, cancel, answer, finished) = Race(read, remaining);
        _spent[name] = used + time.GetElapsedTime(begun);
        if (!finished)
        {
            abandoned[key] = task;   // left running: the read is cancelled, but one that ignores that is only waited for, never repeated
            _ = _failed.Add(key);
            cancel.Cancel();
            _ = task.ContinueWith(_ => cancel.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);   // registered after Cancel: nothing races it
            return (SourceOutcome.TimedOut, default);
        }

        cancel.Dispose();
        var (ok, value) = answer.Value;
        return ok ? (SourceOutcome.Done, value) : (SourceOutcome.Failed, default);
    }

    /// <summary>Starts the read and waits for it or for the injected clock's deadline, whichever comes first. The deadline timer exists before the read starts, so a test that sees the read begin can make it late.</summary>
    private (Task Task, CancellationTokenSource Cancel, StrongBox<(bool Ok, T? Value)> Answer, bool Finished) Race<T>(Func<CancellationToken, T> read, TimeSpan remaining)
    {
        var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var answer = new StrongBox<(bool Ok, T? Value)>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var timer = Task.Delay(remaining, time, deadline.Token);
        var task = Task.Run(() => answer.Value = Attempt(read, cancel.Token), CancellationToken.None);
        try
        {
            _ = Task.WaitAny([task, timer], shutdown);
        }
        finally
        {
            deadline.Cancel();
        }

        return (task, cancel, answer, task.IsCompleted);
    }

    /// <summary>The processes table gets the whole budget; the others a fifth; an environment read at least 250 ms (never more than the budget), so a loaded box doesn't turn healthy pids unknown.</summary>
    private TimeSpan Share(string name) => name switch
    {
        "processes" => budget,
        "environment" => Min(budget, Max(budget / Sources, _environmentFloor)),
        _ => budget / Sources,
    };

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    internal IReadOnlyList<T> List<T>(string name, Func<CancellationToken, Result<IReadOnlyList<T>>> read)
    {
        var (outcome, result) = Run(name, pid: null, read);
        if (outcome == SourceOutcome.Done && result is { IsSuccess: true } ok)
        {
            return ok.Value;
        }

        Note(name);
        return [];
    }

    internal T Value<T>(string name, int pid, Func<CancellationToken, T> read, T fallback)
    {
        var (outcome, value) = Run(name, pid, read);
        if (outcome == SourceOutcome.Done)
        {
            return value ?? fallback;   // a source that answers "nothing" (no cwd) is not a problem
        }

        Note(name);
        return fallback;
    }

    /// <summary>A source read once for the whole pass (no pid): its answer, or <paramref name="fallback"/> with a problem row when it fails or runs late.</summary>
    internal T Value<T>(string name, Func<CancellationToken, T> read, T fallback)
    {
        var (outcome, value) = Run(name, pid: null, read);
        if (outcome == SourceOutcome.Done)
        {
            return value ?? fallback;
        }

        Note(name);
        return fallback;
    }

    private static (bool Ok, T? Value) Attempt<T>(Func<CancellationToken, T> read, CancellationToken cancellationToken)
    {
        try
        {
            return (true, read(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return (false, default);   // the pass gave up on it or the host is stopping
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return (false, default);   // reported as the source's problem row; the message never leaves this method
        }
    }

    internal void Note(string name)
    {
        if (!problems.Exists(p => string.Equals(p.Source, name, StringComparison.Ordinal)))
        {
            problems.Add(SessionProblems.Source(name));
        }
    }
}
