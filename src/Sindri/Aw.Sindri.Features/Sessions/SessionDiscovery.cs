using System.Diagnostics;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass over the sources (collector spec §4.1): the process table, Claude's records, the hook's state files. A source that fails is a problem row and leaves the
/// others intact; when the process table itself fails or overruns the pass is not good and the caller keeps the previous snapshot.
/// </summary>
internal sealed class SessionDiscovery(IProcessTable processes, IClaudeSessionRecords records, IFleetStates fleet, IAgentCatalog catalog, SessionSettings settings)
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly IReadOnlyDictionary<string, string> _emptyEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>False when launcher.json turned the collector off.</summary>
    internal bool IsEnabled() => settings.Enabled;

    /// <summary>The problem rows every snapshot carries: a test override in this run, an invalid launcher.json.</summary>
    internal IReadOnlyList<SessionProblem> StaticProblems()
    {
        var problems = new List<SessionProblem>();
        if (settings.TestOverride)
        {
            problems.Add(SessionProblems.TestOverride);
        }

        if (settings.ConfigProblem is not null)
        {
            problems.Add(SessionProblems.Launcher);
        }

        return problems;
    }

    /// <summary>Deletes dead sessions' hook files after a good pass (collector spec §5.4).</summary>
    internal int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now) => fleet.Prune(live, now);

    /// <summary>The sources still running from an earlier pass that gave up on them: by name, so a blocked source is reported again and never started a second time.</summary>
    private readonly Dictionary<string, Task> _abandoned = new(StringComparer.Ordinal);

    /// <summary>
    /// One pass. The process table gets the whole <paramref name="budget"/> (past it, null: the caller keeps the previous snapshot); every other source gets its own
    /// fifth of it, so one slow source is one problem row and the rest stay live (collector spec §4.1).
    /// </summary>
    internal Discovered? Discover(TimeSpan budget, CancellationToken cancellationToken)
    {
        var problems = new List<SessionProblem>();
        var pass = new Pass(_abandoned, budget, problems, cancellationToken);
        var (outcome, table) = pass.Run("processes", processes.Read);
        if (outcome == Outcome.TimedOut)
        {
            return null;
        }

        if (table is null)
        {
            return new Discovered([], [SessionProblems.Source("processes")], Good: false);
        }

        if (table.IsError)
        {
            return new Discovered([], [SessionProblems.ProcessTable(table.Error)], Good: false);
        }

        var agents = Agents();
        var claudeRecords = pass.List("claude-records", records.Read);
        var hooks = pass.List("fleet-states", fleet.Read);
        var byPid = table.Value.ToDictionary(static p => p.Pid);
        var live = LiveRecords(claudeRecords, byPid);
        var found = AgentProcessRules.Find(table.Value, agents, pid => pass.Value("environment", _ => processes.Environment(pid), _emptyEnvironment).ContainsKey("AW_JUDGE_CHILD"), live.ContainsKey);
        var candidates = new List<Candidate>();
        if (agents.Find(static a => a.Id.Equals(_claude)) is { } claudeAgent)
        {
            candidates.AddRange(live.Values.Select(r => FromRecord(r, byPid[r.Pid], claudeAgent, hooks, pass)));
        }

        candidates.AddRange(found.Where(f => !live.ContainsKey(f.Process.Pid)).Select(f => FromProcess(f, hooks, pass)));
        return new Discovered(candidates, problems, Good: true);
    }

    private enum Outcome
    {
        Done,
        TimedOut,
        Failed,
    }

    /// <summary>
    /// The sources of one pass, each read on its own task under its own deadline. A source that times out or throws is one problem row and an empty answer for the rest of
    /// the pass; one whose earlier read is still running is not started again (no pile-up) and is reported the same way.
    /// </summary>
    private sealed class Pass(Dictionary<string, Task> abandoned, TimeSpan budget, List<SessionProblem> problems, CancellationToken shutdown)
    {
        private const int Sources = 5;
        private readonly Dictionary<string, long> _begun = new(StringComparer.Ordinal);
        private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

        internal (Outcome Outcome, T? Value) Run<T>(string name, Func<CancellationToken, T> read)
        {
            shutdown.ThrowIfCancellationRequested();
            if (_failed.Contains(name) || (abandoned.TryGetValue(name, out var running) && !running.IsCompleted))
            {
                _ = _failed.Add(name);
                return (Outcome.TimedOut, default);
            }

            _ = abandoned.Remove(name);
            if (!_begun.TryGetValue(name, out var begun))
            {
                begun = Stopwatch.GetTimestamp();
                _begun[name] = begun;
            }

            var share = string.Equals(name, "processes", StringComparison.Ordinal) ? budget : budget / Sources;
            var remaining = share - Stopwatch.GetElapsedTime(begun);
            if (remaining <= TimeSpan.Zero)
            {
                _ = _failed.Add(name);
                return (Outcome.TimedOut, default);
            }

            var cancel = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
            var task = Task.Run(() => Attempt(read, cancel.Token), CancellationToken.None);
            _ = task.ContinueWith(_ => cancel.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (!task.Wait(remaining, shutdown))
            {
                abandoned[name] = task;   // left running: the read is cancelled, but one that ignores that is only waited for, never repeated
                _ = _failed.Add(name);
                cancel.Cancel();
                return (Outcome.TimedOut, default);
            }

            var (ok, value) = task.Result;
            return ok ? (Outcome.Done, value) : (Outcome.Failed, default);
        }

        internal IReadOnlyList<T> List<T>(string name, Func<CancellationToken, Result<IReadOnlyList<T>>> read)
        {
            var (outcome, result) = Run(name, read);
            if (outcome == Outcome.Done && result is { IsSuccess: true } ok)
            {
                return ok.Value;
            }

            Note(name);
            return [];
        }

        internal T Value<T>(string name, Func<CancellationToken, T> read, T fallback)
        {
            var (outcome, value) = Run(name, read);
            if (outcome == Outcome.Done)
            {
                return value ?? fallback;   // a source that answers "nothing" (no cwd) is not a problem
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

        private void Note(string name)
        {
            if (!problems.Exists(p => string.Equals(p.Source, name, StringComparison.Ordinal)))
            {
                problems.Add(SessionProblems.Source(name));
            }
        }
    }

    private static Dictionary<int, ClaudeRecord> LiveRecords(IReadOnlyList<ClaudeRecord> records, Dictionary<int, ProcessEntry> byPid)
    {
        var live = new Dictionary<int, ClaudeRecord>();
        foreach (var record in records)
        {
            if (byPid.TryGetValue(record.Pid, out var process) && SessionLiveness.Matches(record.ProcStart, process.StartTicks) && IsSession(record))
            {
                live[record.Pid] = record;
            }
        }

        return live;
    }

    private static bool IsSession(ClaudeRecord record) =>
        string.Equals(record.Kind, "interactive", StringComparison.Ordinal)
        || (string.Equals(record.Entrypoint, "sdk-ts", StringComparison.Ordinal) && record.Cwd?.Contains("/.t3/worktrees", StringComparison.Ordinal) == true);

    private List<AgentEntry> Agents() =>
        [.. catalog.All.Where(a => settings.Providers is null || settings.Providers.Any(p => p.Equals(a.Id)))];

    private Candidate FromRecord(ClaudeRecord record, ProcessEntry process, AgentEntry agent, IReadOnlyList<FleetStateFile> hooks, Pass pass)
    {
        var id = record.Session ?? SessionId.OfProcess(process.Pid, process.StartTicks);
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(agent.Id) && h.Session.Equals(id));
        return new Candidate(process, agent, id, IdKnown: record.Session is not null, pass.Value("environment", _ => processes.Environment(process.Pid), _emptyEnvironment))
        {
            Record = record,
            Hook = hook,
            Cwd = record.Cwd ?? hook?.Cwd ?? pass.Value("cwd", _ => processes.Cwd(process.Pid), fallback: null),
            TranscriptHint = hook?.Transcript,
        };
    }

    private Candidate FromProcess(AgentProcess found, IReadOnlyList<FleetStateFile> hooks, Pass pass)
    {
        var process = found.Process;
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(found.Agent.Id) && h.AgentPid == process.Pid && h.AgentStartTicks == process.StartTicks);
        var files = pass.Value("open-files", _ => processes.OpenFiles(process.Pid), (IReadOnlyList<string>)[]);
        var known = hook?.Session ?? RolloutId(found.Agent, files);
        return new Candidate(process, found.Agent, known ?? SessionId.OfProcess(process.Pid, process.StartTicks), IdKnown: known is not null, pass.Value("environment", _ => processes.Environment(process.Pid), _emptyEnvironment))
        {
            Hook = hook,
            Cwd = hook?.Cwd ?? pass.Value("cwd", _ => processes.Cwd(process.Pid), fallback: null),
            TranscriptHint = hook?.Transcript ?? files.FirstOrDefault(static f => f.EndsWith(".jsonl", StringComparison.Ordinal)),
        };
    }

    private static SessionId? RolloutId(AgentEntry agent, IReadOnlyList<string> files)
    {
        if (agent.IdCapture is not { } pattern)
        {
            return null;
        }

        var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var file in files)
        {
            var match = regex.Match(file);
            if (match.Success && match.Groups.Count > 1 && SessionId.TryFrom(match.Groups[1].Value, out var id))
            {
                return id;
            }
        }

        return null;
    }
}
