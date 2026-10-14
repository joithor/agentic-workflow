namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// The pinned harness probe (engine spec §6.2): one entry per probed harness, each the reduction of its golden report
/// under tests/Aw.Sindri.Tests/Golden/Probe/ ('Agent_variables_match_the_probe' keeps them equal). API spec §6's
/// agent variables and harness processes are read from here (<see cref="AgentSession"/>). How to re-probe and pin a
/// harness: docs/csharp/sindri.md, "The harness probe".
/// </summary>
internal static class PinnedProbe
{
    private static readonly Dictionary<string, string> _none = [];

    /// <summary>Why the variables Claude Code sets but that don't mark it were left out (docs/csharp/sindri.md).</summary>
    private static readonly Dictionary<string, string> _claudeExcluded = new(StringComparer.Ordinal)
    {
        ["AI_AGENT"] = "a cross-tool marker, not Claude Code's own; an agent-aware app that also hosts the human's terminal (Cursor) may set it there too, so it waits for the cursor-terminal probe",
        ["CLAUDE_CODE_BRIDGE_SESSION_ID"] = "set only when the session is bridged to claude.ai, not in every Claude Code session",
        ["CLAUDE_CODE_CHILD_SESSION"] = "set only in some launch modes, not in every Claude Code session",
        ["CLAUDE_CODE_MESSAGING_SOCKET"] = "set only when session messaging is on, not in every Claude Code session",
        ["CLAUDE_CODE_MESSAGING_TOKEN"] = "set only when session messaging is on, not in every Claude Code session",
        ["CLAUDE_CODE_SESSION_ATTENDED"] = "set only in some launch modes, not in every Claude Code session",
        ["CLAUDE_EFFORT"] = "a Claude Code setting a human may export in their own shell",
        ["COREPACK_ENABLE_AUTO_PIN"] = "Node corepack's own setting; humans set it too",
        ["GIT_EDITOR"] = "git's own setting; humans set it too",
        ["NoDefaultCurrentDirectoryInExePath"] = "a Windows/Node path-search setting; humans set it too",
        ["AW_SINDRI_HOME"] = "set by scripts/sindri-prove.sh for the probe run, not by Claude Code",
        ["AW_SINDRI_PORT"] = "set by scripts/sindri-prove.sh for the probe run, not by Claude Code",
        ["SINDRI"] = "the prover's command line, set for the probe run, not by Claude Code",
    };

    /// <summary>Every pinned harness, in the order 'doctor' prints them.</summary>
    internal static IReadOnlyList<PinnedHarness> All { get; } =
    [
        new("claude-code", "claude-code", Human: false, Pending: false, "claude", "2.1.299",
            ["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_EXECPATH", "CLAUDE_CODE_SESSION_ID", "CLAUDE_PID"],
            [new HarnessProcess("claude", ExecutablePart: null), new HarnessProcess(Name: null, "/claude/versions/")],
            _claudeExcluded),
        Owner("codex:read-only", "codex", "codex", ["CODEX_SANDBOX"], [new HarnessProcess("codex", ExecutablePart: null)]),
        Owner("codex:workspace-write", "codex", "codex", ["CODEX_SANDBOX"], [new HarnessProcess("codex", ExecutablePart: null)]),
        Owner("codex:danger-full-access", "codex", "codex", ["CODEX_SANDBOX"], [new HarnessProcess("codex", ExecutablePart: null)]),
        Owner("cursor-agent", "cursor", "cursor-agent", ["CURSOR_AGENT"], [new HarnessProcess("cursor-agent", ExecutablePart: null)]),
        Owner("cursor-terminal", "cursor", "cursor", [], []) with { Human = true },
        new("plain-terminal", "terminal", Human: true, Pending: false, VersionCommand: null, Version: null, [], [], _none),
    ];

    /// <summary>The agent variables API spec §6 reads: every marker, in pin order, each once.</summary>
    internal static IReadOnlyList<string> Variables { get; } = [.. All.SelectMany(h => h.Markers).Distinct(StringComparer.Ordinal)];

    /// <summary>The ancestry patterns that make a parent process an agent harness.</summary>
    internal static IReadOnlyList<HarnessProcess> Processes { get; } = [.. All.SelectMany(h => h.Processes).Distinct()];

    /// <summary>
    /// The harness this process runs in, by the pinned markers and ancestry: one entry's name when exactly one matches,
    /// the family's when several of one family do (Codex's sandbox modes look alike), else 'unknown'.
    /// </summary>
    /// <param name="variables">The variables set.</param>
    /// <param name="ancestry">The ancestors.</param>
    /// <returns>The harness name.</returns>
    internal static string Recognise(IReadOnlyCollection<string> variables, IReadOnlyList<ProbedProcess> ancestry)
    {
        var matched = All.Where(h => !h.Human
            && (h.Markers.Any(m => variables.Contains(m, StringComparer.Ordinal)) || ancestry.Any(a => h.Processes.Any(p => p.Matches(a))))).ToList();
        return matched switch
        {
            [var one] => one.Harness,
            [var first, ..] when matched.TrueForAll(h => string.Equals(h.Family, first.Family, StringComparison.Ordinal)) => first.Family,
            _ => "unknown",
        };
    }

    /// <summary>An owner probe not run yet: API spec §6's provisional markers until the owner pins the real ones.</summary>
    private static PinnedHarness Owner(string harness, string family, string command, IReadOnlyList<string> markers, IReadOnlyList<HarnessProcess> processes) =>
        new(harness, family, Human: false, Pending: true, command, Version: null, markers, processes, _none);
}
