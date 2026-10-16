using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass over the sources (collector spec §4.1): the process table, Claude's records, the hook's state files. A source that fails is a problem row and leaves the
/// others intact; when the process table itself fails the pass is not good and the caller keeps the previous snapshot.
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

    internal Discovered Discover(CancellationToken cancellationToken)
    {
        var table = processes.Read(cancellationToken);
        if (table.IsError)
        {
            return new Discovered([], [SessionProblems.ProcessTable(table.Error)], Good: false);
        }

        var problems = new List<SessionProblem>();
        var agents = Agents();
        var guard = new Guard(problems);
        var claudeRecords = guard.Result(() => records.Read(cancellationToken), "claude-records");
        var hooks = guard.Result(() => fleet.Read(cancellationToken), "fleet-states");
        var byPid = table.Value.ToDictionary(static p => p.Pid);
        var live = LiveRecords(claudeRecords, byPid);
        var found = AgentProcessRules.Find(table.Value, agents, pid => guard.Run(() => processes.Environment(pid), "environment", _emptyEnvironment).ContainsKey("AW_JUDGE_CHILD"), live.ContainsKey);
        var candidates = new List<Candidate>();
        if (agents.Find(static a => a.Id.Equals(_claude)) is { } claudeAgent)
        {
            candidates.AddRange(live.Values.Select(r => FromRecord(r, byPid[r.Pid], claudeAgent, hooks, guard)));
        }

        candidates.AddRange(found.Where(f => !live.ContainsKey(f.Process.Pid)).Select(f => FromProcess(f, hooks, guard)));
        return new Discovered(candidates, problems, Good: true);
    }

    /// <summary>One source failing (an error result or an exception) is one problem row for that source and an empty answer; the other sources stay intact (collector spec §4.1).</summary>
    private sealed class Guard(List<SessionProblem> problems)
    {
        internal IReadOnlyList<T> Result<T>(Func<Result<IReadOnlyList<T>>> read, string name)
        {
            try
            {
                var result = read();
                if (result.IsSuccess)
                {
                    return result.Value;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // reported below, the same as an error result
            }

            Note(name);
            return [];
        }

        internal T Run<T>(Func<T> read, string name, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Note(name);
                return fallback;
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

    private Candidate FromRecord(ClaudeRecord record, ProcessEntry process, AgentEntry agent, IReadOnlyList<FleetStateFile> hooks, Guard guard)
    {
        var id = record.Session ?? SessionId.OfProcess(process.Pid, process.StartTicks);
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(agent.Id) && h.Session.Equals(id));
        return new Candidate(process, agent, id, IdKnown: record.Session is not null, guard.Run(() => processes.Environment(process.Pid), "environment", _emptyEnvironment))
        {
            Record = record,
            Hook = hook,
            Cwd = record.Cwd ?? hook?.Cwd ?? guard.Run(() => processes.Cwd(process.Pid), "cwd", fallback: null),
            TranscriptHint = hook?.Transcript,
        };
    }

    private Candidate FromProcess(AgentProcess found, IReadOnlyList<FleetStateFile> hooks, Guard guard)
    {
        var process = found.Process;
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(found.Agent.Id) && h.AgentPid == process.Pid && h.AgentStartTicks == process.StartTicks);
        var files = guard.Run(() => processes.OpenFiles(process.Pid), "open-files", []);
        var known = hook?.Session ?? RolloutId(found.Agent, files);
        return new Candidate(process, found.Agent, known ?? SessionId.OfProcess(process.Pid, process.StartTicks), IdKnown: known is not null, guard.Run(() => processes.Environment(process.Pid), "environment", _emptyEnvironment))
        {
            Hook = hook,
            Cwd = hook?.Cwd ?? guard.Run(() => processes.Cwd(process.Pid), "cwd", fallback: null),
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
