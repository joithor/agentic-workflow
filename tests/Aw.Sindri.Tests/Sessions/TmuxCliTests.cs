using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sessions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The one place Sindri runs tmux, against a stand-in program that records what it was called with (collector spec CI-3, ARCH-SESS-001). Never a real tmux.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class TmuxCliTests : IDisposable
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(2);

    private readonly string _directory = Directory.CreateTempSubdirectory("aw-tmux-standin").FullName;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    private string Log => Path.Combine(_directory, "calls.log");

    private TmuxCli Cli()
    {
        var script = Path.Combine(_directory, "tmux");
        var lines = new[]
        {
            $"if [ -f '{_directory}/hang' ]; then : > '{_directory}/started'; exec sleep 30; fi",
            $"{{ printf 'ARGS'; for a in \"$@\"; do printf ' [%s]' \"$a\"; done; echo; env | grep '^TMUX' | sed 's/^/ENV /'; }} >> '{Log}'",
            "case \"$3\" in",
            "  list-panes) printf '4242\\tapi\\t0\\t1\\t%%5\\nbroken row\\n' ;;",
            "  list-clients) printf '777\\t/dev/pts/3\\tapi\\n' ;;",
            "esac",
        };
        _ = ExecutableScript.Write(script, string.Join('\n', lines) + "\n");
        return new TmuxCli(_clock, script, _budget);
    }

    private List<string> Calls() => File.Exists(Log) ? [.. File.ReadAllLines(Log)] : [];

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_passes_S_and_drops_TMUX()
    {
        var before = (Environment.GetEnvironmentVariable("TMUX"), Environment.GetEnvironmentVariable("TMUX_PANE"));
        Environment.SetEnvironmentVariable("TMUX", "/tmp/tmux-1000/leaked,1,0");
        Environment.SetEnvironmentVariable("TMUX_PANE", "%9");
        try
        {
            var view = Cli().Read("/tmp/tmux-1000/t1", TestContext.Current.CancellationToken);

            Assert.True(view.IsSuccess);
            Assert.Equal(["ARGS [-S] [/tmp/tmux-1000/t1] [list-panes] [-a] [-F] [#{pane_pid}\t#{session_name}\t#{window_index}\t#{pane_index}\t#{pane_id}]",
                "ARGS [-S] [/tmp/tmux-1000/t1] [list-clients] [-F] [#{client_pid}\t#{client_tty}\t#{client_session}]"], Calls());   // no ENV line: neither TMUX nor TMUX_PANE reached the child
            Assert.Equal((4242, "api", 0, 1, "%5"), (view.Value.Panes[0].PanePid, view.Value.Panes[0].Session, view.Value.Panes[0].Window, view.Value.Panes[0].Index, view.Value.Panes[0].Pane));
            _ = Assert.Single(view.Value.Panes);   // the row that is not five fields is dropped
            Assert.Equal((777, "/dev/pts/3", "api"), (view.Value.Clients[0].Pid, view.Value.Clients[0].Tty, view.Value.Clients[0].Session));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMUX", before.Item1);
            Environment.SetEnvironmentVariable("TMUX_PANE", before.Item2);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_keeps_an_answer_for_10_s()
    {
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;
        _ = cli.Read("/s", token);

        _clock.Advance(TmuxCli.Window - TimeSpan.FromSeconds(1));
        _ = cli.Read("/s", token);
        var cached = Calls().Count;
        _clock.Advance(TimeSpan.FromSeconds(2));
        _ = cli.Read("/s", token);

        Assert.Equal((2, 4), (cached, Calls().Count));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task TmuxCli_skips_a_timed_out_socket_for_10s()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "hang"), string.Empty, TestContext.Current.CancellationToken);
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;

        var first = Task.Run(() => cli.Read("/s", token), token);
        await WaitFor("started", token);
        _clock.Advance(_budget + TimeSpan.FromMilliseconds(1));
        var timedOut = await first;
        File.Delete(Path.Combine(_directory, "started"));
        _clock.Advance(TimeSpan.FromSeconds(5));
        var skipped = cli.Read("/s", token);
        var askedAgain = File.Exists(Path.Combine(_directory, "started"));
        _clock.Advance(TimeSpan.FromSeconds(6));
        File.Delete(Path.Combine(_directory, "hang"));
        var later = cli.Read("/s", token);

        Assert.False(timedOut.IsSuccess);
        Assert.False(skipped.IsSuccess);
        Assert.False(askedAgain);   // inside the 10 s window the socket isn't asked
        Assert.True(later.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void TmuxCli_production_budget_is_one_second() =>
        Assert.Equal(TimeSpan.FromSeconds(1), TmuxCli.CallBudget);   // collector spec CO9

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_refuses_a_socket_that_is_not_an_absolute_path()
    {
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;

        Assert.False(cli.Read("t1", token).IsSuccess);
        Assert.False(cli.Read("@abstract-name", token).IsSuccess);
        Assert.Empty(Calls());   // nothing was spawned
    }

    [Fact, Trait("Lane", "unit")]
    public void TmuxCli_without_a_tmux_program_answers_failed()
    {
        var cli = new TmuxCli(_clock, Path.Combine(_directory, "no-such-tmux"), _budget);

        Assert.False(cli.Read("/s", TestContext.Current.CancellationToken).IsSuccess);
    }

    private async Task WaitFor(string marker, CancellationToken token)
    {
        for (var i = 0; i < 200 && !File.Exists(Path.Combine(_directory, marker)); i++)
        {
            await Task.Delay(50, token);
        }

        Assert.True(File.Exists(Path.Combine(_directory, marker)), "the stand-in never started");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
