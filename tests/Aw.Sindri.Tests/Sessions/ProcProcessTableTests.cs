using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The /proc adapter over the fixture tree (collector spec §4.1 steps 1, 2, 5, 6).</summary>
public sealed class ProcProcessTableTests
{
    private static ProcProcessTable Fixture(int uid = SessionFixtures.OwnUid) => new(SessionFixtures.Proc, uid);

    private static IReadOnlyList<ProcessEntry> Table(int uid = SessionFixtures.OwnUid) => Fixture(uid).Read(TestContext.Current.CancellationToken).Value;

    [Fact, Trait("Lane", "integration")]
    public void The_table_holds_the_fixture_users_live_processes_and_skips_zombies_and_other_users()
    {
        var pids = Table().Select(static p => p.Pid).ToList();

        Assert.DoesNotContain(1200, pids);   // a zombie
        Assert.DoesNotContain(1300, pids);   // another user's, by status Uid:
        Assert.Contains(211, pids);
        Assert.Equal([1300], Table(uid: 1001).Select(static p => p.Pid));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_table_reads_stat_argv0_and_the_exe_link()
    {
        var claude = Table().Single(static p => p.Pid == 211);

        Assert.Equal((210, "claude", 34816, 1100L, "claude"), (claude.ParentPid, claude.Comm, claude.Tty, claude.StartTicks, claude.Argv0));
        Assert.Equal("/nonexistent-fixture-root/home/dev/.local/share/claude/versions/2.1.301/claude", claude.ExePath);
        Assert.Equal(0, Table().Single(static p => p.Pid == 212).Tty);
    }

    [Fact, Trait("Lane", "integration")]
    public void Cmdline_is_read_to_the_first_nul_only()
    {
        var judge = Table().Single(static p => p.Pid == 212);
        var directory = Directory.CreateTempSubdirectory("aw-cmdline").FullName;
        try
        {
            var file = Path.Combine(directory, "cmdline");
            File.WriteAllBytes(file, "claude\0-p\0this prompt text must never be read\0"u8.ToArray());

            Assert.Equal("claude", ProcFiles.ArgvZero(file));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Assert.Equal("claude", judge.Argv0);
        Assert.DoesNotContain(Table(), static p => (p.Argv0 ?? "").Contains("prompt", StringComparison.Ordinal) || (p.ExePath ?? "").Contains("prompt", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void Environ_keeps_only_the_allowlist_and_a_planted_token_reaches_nothing()
    {
        var environment = Fixture().Environment(211);

        Assert.Equal(["TERM_PROGRAM", "WARP_FOCUS_URL"], environment.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(ProcessTableContract.PlantedToken, environment.Values, StringComparer.Ordinal);
        Assert.Contains("AW_JUDGE_CHILD", Fixture().Environment(212).Keys, StringComparer.Ordinal);
        Assert.Equal("LCH-00000000000000000000000000000001", Fixture().Environment(411)["AW_SINDRI_LAUNCH"]);
    }

    [Fact, Trait("Lane", "integration")]
    public void Cwd_and_open_files_come_from_the_links()
    {
        Assert.Equal("/nonexistent-fixture-root/home/dev/codex-repo", Fixture().Cwd(600));
        Assert.Contains(Fixture().OpenFiles(600), static f => f.EndsWith("0199a1b2-c3d4-7e5f-8a6b-7c8d9e0f1a2b.jsonl", StringComparison.Ordinal));
        Assert.Contains("socket:[880001]", Fixture().OpenFiles(400), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_missing_root_is_an_unsupported_platform()
    {
        var read = new ProcProcessTable("/nonexistent-fixture-root/proc", SessionFixtures.OwnUid).Read(TestContext.Current.CancellationToken);

        Assert.Equal(("SND-SESSION-002", "unsupported-platform"), (read.Error.Code, read.Error.Message));
    }
}
