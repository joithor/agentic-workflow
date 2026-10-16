using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass over the sources (collector spec §4.1): the process table, Claude's records, the hook's state files. A source that fails is a problem row and leaves the
/// others intact; when the process table itself fails the pass is not good and the caller keeps the previous snapshot.
/// </summary>
internal sealed class SessionDiscovery
{
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private readonly IProcessTable _processes;
    private readonly IClaudeSessionRecords _records;
    private readonly IFleetStates _fleet;
    private readonly IAgentCatalog _catalog;
    private readonly SessionSettings _settings;

    internal SessionDiscovery(IProcessTable processes, IClaudeSessionRecords claude, IFleetStates fleet, IAgentCatalog catalog, SessionSettings settings)
    {
        _processes = processes;
        _records = claude;
        _fleet = fleet;
        _catalog = catalog;
        _settings = settings;
    }

    /// <summary>False when launcher.json turned the collector off.</summary>
    internal bool Enabled => _settings.Enabled;

    /// <summary>The problem rows every snapshot carries: a test override in this run, an invalid launcher.json.</summary>
    internal IReadOnlyList<SessionProblem> StaticProblems
    {
        get
        {
            var problems = new List<SessionProblem>();
            if (_settings.TestOverride)
            {
                problems.Add(SessionProblems.TestOverride);
            }

            if (_settings.ConfigProblem is not null)
            {
                problems.Add(SessionProblems.Launcher);
            }

            return problems;
        }
    }

    /// <summary>Deletes dead sessions' hook files after a good pass (collector spec §5.4).</summary>
    internal int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now) => _fleet.Prune(live, now);

    internal Discovered Discover(CancellationToken cancellationToken)
    {
        var table = _processes.Read(cancellationToken);
        if (table.IsError)
        {
            return new Discovered([], [SessionProblems.ProcessTable(table.Error)], Good: false);
        }

        var problems = new List<SessionProblem>();
        var agents = Agents();
        var records = Source(_records.Read(cancellationToken), "claude-records", problems);
        var hooks = Source(_fleet.Read(cancellationToken), "fleet-states", problems);
        var byPid = table.Value.ToDictionary(static p => p.Pid);
        var live = LiveRecords(records, byPid);
        var found = AgentProcessRules.Find(table.Value, agents, pid => _processes.Environment(pid).ContainsKey("AW_JUDGE_CHILD"), live.ContainsKey);
        var candidates = new List<Candidate>();
        if (agents.Find(static a => a.Id.Equals(_claude)) is { } claudeAgent)
        {
            candidates.AddRange(live.Values.Select(r => FromRecord(r, byPid[r.Pid], claudeAgent, hooks)));
        }

        candidates.AddRange(found.Where(f => !live.ContainsKey(f.Process.Pid)).Select(f => FromProcess(f, hooks)));
        return new Discovered(candidates, problems, Good: true);
    }

    private static IReadOnlyList<T> Source<T>(Result<IReadOnlyList<T>> read, string name, List<SessionProblem> problems)
    {
        if (read.IsSuccess)
        {
            return read.Value;
        }

        problems.Add(SessionProblems.Source(name));
        return [];
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
        [.. _catalog.All.Where(a => _settings.Providers is null || _settings.Providers.Any(p => p.Equals(a.Id)))];

    private Candidate FromRecord(ClaudeRecord record, ProcessEntry process, AgentEntry agent, IReadOnlyList<FleetStateFile> hooks)
    {
        var id = record.Session ?? SessionId.OfProcess(process.Pid, process.StartTicks);
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(agent.Id) && h.Session.Equals(id));
        return new Candidate(process, agent, id, IdKnown: record.Session is not null, _processes.Environment(process.Pid))
        {
            Record = record,
            Hook = hook,
            Cwd = record.Cwd ?? hook?.Cwd ?? _processes.Cwd(process.Pid),
            TranscriptHint = hook?.Transcript,
        };
    }

    private Candidate FromProcess(AgentProcess found, IReadOnlyList<FleetStateFile> hooks)
    {
        var process = found.Process;
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(found.Agent.Id) && h.AgentPid == process.Pid && h.AgentStartTicks == process.StartTicks);
        var files = _processes.OpenFiles(process.Pid);
        var known = hook?.Session ?? RolloutId(found.Agent, files);
        return new Candidate(process, found.Agent, known ?? SessionId.OfProcess(process.Pid, process.StartTicks), IdKnown: known is not null, _processes.Environment(process.Pid))
        {
            Hook = hook,
            Cwd = hook?.Cwd ?? _processes.Cwd(process.Pid),
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
