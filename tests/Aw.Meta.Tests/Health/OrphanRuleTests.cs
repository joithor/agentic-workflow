using Aw.Meta.Core.Health;

namespace Aw.Meta.Tests.Health;

public sealed class OrphanRuleTests
{
    private const string Temporal = "temporal server start-dev --port 7233 --db-filename /tmp/aw-sindri-ab12/temporal.db";

    private static ProcessInfo Process(string cmdline = Temporal, int ppid = 2200, long age = 900) => new(4242, ppid, cmdline, RssKb: 100, age);

    [Theory, Trait("Lane", "unit")]
    [InlineData("systemd --user", true)]
    [InlineData("/usr/lib/systemd/systemd --user", true)]
    [InlineData("bash -l", false)]
    [InlineData(null, false)]
    public void The_parent_must_be_the_user_manager(string? parent, bool orphan) =>
        Assert.Equal(orphan, OrphanRule.IsOrphan(Process(), parent));

    [Fact, Trait("Lane", "unit")]
    public void A_process_reparented_to_init_counts() =>
        Assert.True(OrphanRule.IsOrphan(Process(ppid: 1), parentCmdline: null));

    [Theory, Trait("Lane", "unit")]
    [InlineData("temporal server start-dev --db-filename=/tmp/aw-sindri-x/db", true)]
    [InlineData("temporal server start-dev --db-filename /home/me/dev.db", false)]
    [InlineData("temporal server start-dev", false)]
    [InlineData("temporal operator namespace list --db-filename /tmp/aw-sindri-x/db", false)]
    [InlineData("sleep 1000", false)]
    public void Only_scratch_temporal_dev_servers_qualify(string cmdline, bool orphan) =>
        Assert.Equal(orphan, OrphanRule.IsOrphan(Process(cmdline), "systemd --user"));

    [Theory, Trait("Lane", "unit")]
    [InlineData(301, true)]
    [InlineData(300, false)]
    [InlineData(10, false)]
    public void It_must_be_older_than_five_minutes(long age, bool orphan) =>
        Assert.Equal(orphan, OrphanRule.IsOrphan(Process(age: age), "systemd --user"));
}
