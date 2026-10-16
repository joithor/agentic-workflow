using Aw.Sindri.Core.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Which processes are agent sessions (collector spec §4.1 step 2).</summary>
public sealed class AgentProcessRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void AgentProcess_matches_argv0_then_exe_then_comm()
    {
        // argv[0] wins even when the kernel's name is the version file (Claude's release names its binary after its version)
        Assert.Same(Claude, AgentProcessRules.Match(Proc(1, 0, "2.1.301", argv0: "claude"), Agents));
        // the exe path part, when argv[0] is something else
        Assert.Same(Claude, AgentProcessRules.Match(Proc(2, 0, "node", argv0: "node", exe: "/x/.local/share/claude/versions/2.1.301/claude"), Agents));
        // comm, when nothing else is readable
        Assert.Same(Aider, AgentProcessRules.Match(Proc(3, 0, "aider", argv0: "/usr/bin/python3", exe: "/usr/bin/python3.12"), Agents));
        Assert.Null(AgentProcessRules.Match(Proc(4, 0, "bash", argv0: "bash", exe: "/usr/bin/bash"), Agents));
    }

    [Fact, Trait("Lane", "unit")]
    public void AgentProcess_matches_comm_only_for_names_the_kernel_did_not_cut()
    {
        var long_ = new Aw.Sindri.Core.Launch.AgentEntry(Aw.Sindri.Core.Launch.AgentId.From("longname"), "Long", "a-very-long-agent-name", ProcessName: null, Transcripts: null, Resume: null);
        Assert.Null(AgentProcessRules.Match(Proc(5, 0, "a-very-long-age"), [long_]));
        Assert.Same(long_, AgentProcessRules.Match(Proc(5, 0, "x", argv0: "/opt/a-very-long-agent-name"), [long_]));
    }

    [Fact, Trait("Lane", "unit")]
    public void AgentProcess_matches_cursor_only_with_an_exe_path_containing_cursor()
    {
        Assert.Same(Cursor, AgentProcessRules.Match(Proc(6, 0, "agent", argv0: "agent", exe: "/h/.local/share/cursor-agent/versions/1/agent"), Agents));
        Assert.Null(AgentProcessRules.Match(Proc(7, 0, "agent", argv0: "agent", exe: "/usr/local/bin/agent"), Agents));
        Assert.Null(AgentProcessRules.Match(Proc(8, 0, "agent", argv0: "agent"), Agents));
    }

    [Fact, Trait("Lane", "unit")]
    public void AgentProcess_skips_a_claude_p_under_judge()
    {
        var table = new[] { Proc(1, 0, "bash", argv0: "bash"), Proc(10, 1, "claude", argv0: "claude"), Proc(11, 10, "claude", argv0: "claude", tty: 0) };

        var found = AgentProcessRules.Find(table, Agents, isJudgeChild: pid => pid == 11, hasRecord: _ => false);

        Assert.Equal([10], found.Select(f => f.Process.Pid));
        // and with no judge marker at all, a terminal-less claude with no record is not a session
        var plain = AgentProcessRules.Find([Proc(12, 1, "claude", argv0: "claude", tty: 0)], Agents, _ => false, _ => false);
        Assert.Empty(plain);
        _ = Assert.Single(AgentProcessRules.Find([Proc(12, 1, "claude", argv0: "claude", tty: 0)], Agents, _ => false, hasRecord: _ => true));
    }

    [Fact, Trait("Lane", "unit")]
    public void AgentProcess_skips_a_descendant_of_the_same_agent()
    {
        var table = new[] { Proc(10, 1, "claude", argv0: "claude"), Proc(20, 10, "bash", argv0: "bash"), Proc(21, 20, "claude", argv0: "claude"), Proc(30, 1, "codex", argv0: "codex") };

        var found = AgentProcessRules.Find(table, Agents, _ => false, _ => false);

        Assert.Equal([10, 30], found.Select(f => f.Process.Pid).Order());
    }
}
