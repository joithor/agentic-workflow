using Aw.Meta.Core.Health;

namespace Aw.Meta.Tests.Health;

public sealed class HostRulesTests
{
    private const string Temporal = "temporal server start-dev --db-filename /tmp/aw-sindri-ab/t.db";
    private const string Headroom = "node /x/headroom/mcp.js";

    private static ProcessInfo P(int pid, string cmd, int ppid = 100, long rssKb = 2048, long age = 900) => new(pid, ppid, cmd, rssKb, age);

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude --resume x", "claude")]
    [InlineData("/home/u/.local/bin/claude", "claude")]
    [InlineData("node /usr/lib/claude-code/claude --flag", "claude")]
    [InlineData("dotnet build x.slnx", "dotnet")]
    [InlineData("/usr/share/dotnet/dotnet /sdk/MSBuild.dll -nodereuse", "dotnet")]
    [InlineData("/usr/bin/dotnet exec VBCSCompiler.dll", "dotnet")]
    [InlineData("temporal server start-dev", "temporal")]
    [InlineData("/home/u/.local/bin/temporal server start-dev", "temporal")]
    [InlineData(Headroom, "headroom/mcp")]
    [InlineData("npx serena-agent start", "headroom/mcp")]
    [InlineData("node /x/context7-mcp", "headroom/mcp")]
    [InlineData("/usr/bin/qemu-system-x86_64 -m 4096", "docker/qemu")]
    [InlineData("dockerd -H fd://", "docker/qemu")]
    [InlineData("java -jar opensearch.jar", "onyx/opensearch/vespa")]
    [InlineData("vespa-start-services", "onyx/opensearch/vespa")]
    [InlineData("node server.js", "node")]
    [InlineData("bash -l", "other")]
    public void A_process_is_grouped_by_its_program_and_command_line(string cmdline, string group) =>
        Assert.Equal(group, ProcessGroups.Of(P(1, cmdline)));

    [Fact, Trait("Lane", "unit")]
    public void Groups_count_and_size_in_a_fixed_order_and_leave_out_empty_ones()
    {
        var groups = ProcessGroups.Group([P(1, "bash", rssKb: 1024), P(2, "claude", rssKb: 204_800), P(3, "claude", rssKb: 102_400), P(4, "dotnet build", rssKb: 51_200)]);

        Assert.Equal([new ProcessGroup("claude", 2, 300), new ProcessGroup("dotnet", 1, 50), new ProcessGroup("other", 1, 1)], groups);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_orphan_is_found_through_its_parent_in_the_same_snapshot()
    {
        var orphans = HostRules.Orphans(
        [
            P(10, "systemd --user", ppid: 1),
            P(11, Temporal, ppid: 10),
            P(12, Temporal, ppid: 99),   // parent is not the user manager
            P(13, Temporal, ppid: 1),
            P(14, Temporal, ppid: 10, age: 30),
        ]);

        Assert.Equal([11, 13], orphans.Select(p => p.Pid));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_server_with_more_copies_than_sessions_plus_one_is_a_duplicate()
    {
        List<ProcessInfo> processes = [P(1, "claude"), P(2, "claude"), .. Enumerable.Range(10, 3).Select(pid => P(pid, Headroom)), .. Enumerable.Range(20, 4).Select(pid => P(pid, "node /x/other-mcp"))];

        var duplicates = HostRules.Duplicates(processes);

        Assert.Equal([new DuplicateServer("node /x/other-mcp", 4, 3)], duplicates);
    }
}
