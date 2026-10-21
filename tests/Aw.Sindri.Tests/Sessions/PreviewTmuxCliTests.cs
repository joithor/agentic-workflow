using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>
/// The preview's two tmux calls (collector spec CA-9, CI-5, CI-10) against a stand-in program that records its argv and environment: the exact argv, the socket
/// flag before every verb, the 64 KiB tail, the one 1 s deadline and the preview's own timeout mark. Never a real tmux.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class PreviewTmuxCliTests : IDisposable
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(2);

    private readonly string _directory = Directory.CreateTempSubdirectory("aw-preview-standin").FullName;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    private string Log => Path.Combine(_directory, "calls.log");

    private string Marker(string name) => Path.Combine(_directory, name);

    private TmuxCli Cli()
    {
        var script = Path.Combine(_directory, "tmux");
        var lines = new[]
        {
            $"if [ -f '{Marker("hang")}' ]; then : > '{Marker("started")}'; exec sleep 30; fi",
            $"if [ \"$3\" = capture-pane ] && [ -f '{Marker("hang-capture")}' ]; then : > '{Marker("capture-started")}'; exec sleep 30; fi",
            $"if [ \"$3\" = display-message ] && [ -f '{Marker("hold-display")}' ]; then : > '{Marker("display-started")}'; while [ ! -f '{Marker("go")}' ]; do sleep 0.05; done; fi",
            $"{{ printf 'ARGS'; for a in \"$@\"; do printf ' [%s]' \"$a\"; done; echo; env | grep '^TMUX' | sed 's/^/ENV /'; }} >> '{Log}'",
            "case \"$3\" in",
            "  display-message) echo 4242 ;;",
            "  list-panes) printf '4242\\tapi\\t0\\t1\\t%%5\\n' ;;",
            "  list-clients) printf '777\\t/dev/pts/3\\tapi\\n' ;;",
            $"  capture-pane) cat '{Marker("capture.out")}' ;;",
            "esac",
        };
        _ = ExecutableScript.Write(script, string.Join('\n', lines) + "\n");
        File.WriteAllText(Marker("capture.out"), "first\nsecond\n");
        return new TmuxCli(_clock, script, _budget);
    }

    private List<string> Calls() => File.Exists(Log) ? [.. File.ReadAllLines(Log)] : [];

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_capture_pane_passes_S_and_the_exact_argv()
    {
        var before = (Environment.GetEnvironmentVariable("TMUX"), Environment.GetEnvironmentVariable("TMUX_PANE"));
        Environment.SetEnvironmentVariable("TMUX", "/tmp/tmux-1000/leaked,1,0");
        Environment.SetEnvironmentVariable("TMUX_PANE", "%9");
        try
        {
            var read = Cli().Read("/tmp/tmux-1000/t1", "%5", TestContext.Current.CancellationToken);

            Assert.Equal(PaneReadKind.Captured, read.Kind);
            Assert.Equal((4242, "first\nsecond\n"), (read.PanePid, Encoding.UTF8.GetString(read.Capture.Span)));
            Assert.Equal(
                ["ARGS [-S] [/tmp/tmux-1000/t1] [display-message] [-p] [-t] [%5] [#{pane_pid}]",
                    "ARGS [-S] [/tmp/tmux-1000/t1] [capture-pane] [-p] [-J] [-S] [-40] [-t] [%5]"],
                Calls());   // no ENV line: neither TMUX nor TMUX_PANE reached a child
        }
        finally
        {
            Environment.SetEnvironmentVariable("TMUX", before.Item1);
            Environment.SetEnvironmentVariable("TMUX_PANE", before.Item2);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_puts_the_socket_flag_before_the_verb_for_all_five_verbs()
    {
        var cli = Cli();
        var verbs = Enum.GetValues<TmuxVerb>();

        foreach (var verb in verbs)
        {
            _ = cli.Run("/tmp/tmux-1000/t1", verb, [], TestContext.Current.CancellationToken);
        }

        Assert.Equal(5, verbs.Length);
        Assert.Equal(["list-sessions", "list-panes", "list-clients", "display-message", "capture-pane"], Calls().Select(static c => c.Split(']')[2].TrimStart(' ', '[')), StringComparer.Ordinal);
        Assert.All(Calls(), static call => Assert.StartsWith("ARGS [-S] [/tmp/tmux-1000/t1] [", call, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void TmuxCli_keeps_only_the_tail_of_a_large_capture()
    {
        var cli = Cli();
        var big = string.Concat(Enumerable.Range(0, 40_000).Select(static i => string.Create(CultureInfo.InvariantCulture, $"row-{i:D5}\n")));   // 400 KB
        File.WriteAllText(Marker("capture.out"), big);

        var read = cli.Read("/s", "%5", TestContext.Current.CancellationToken);

        Assert.Equal(PreviewRules.MaxBytes + 1, read.Capture.Length);
        Assert.Equal(big[^(PreviewRules.MaxBytes + 1)..], Encoding.UTF8.GetString(read.Capture.Span));
    }

    [Fact, Trait("Lane", "integration")]
    public void Preview_reader_answers_gone_without_asking_for_a_relative_socket_or_a_bad_pane_id()
    {
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(PaneReadKind.Gone, cli.Read("t1", "%5", token).Kind);
        Assert.Equal(PaneReadKind.Gone, cli.Read("/s", "main:0.1", token).Kind);
        Assert.Equal(PaneReadKind.Gone, cli.Read("/s", "%5 ; send-keys", token).Kind);
        Assert.Empty(Calls());
    }

    [Fact, Trait("Lane", "integration")]
    public void Preview_reader_answers_gone_without_a_tmux_program()
    {
        var cli = new TmuxCli(_clock, Path.Combine(_directory, "no-such-tmux"), _budget);

        Assert.Equal(PaneReadKind.Gone, cli.Read("/s", "%5", TestContext.Current.CancellationToken).Kind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Preview_answers_timed_out_on_timeout()
    {
        await File.WriteAllTextAsync(Marker("hang"), string.Empty, TestContext.Current.CancellationToken);
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;

        var pending = Task.Run(() => cli.Read("/s", "%5", token), token);
        await WaitFor("started", token);
        _clock.Advance(_budget + TimeSpan.FromMilliseconds(1));

        Assert.Equal(PaneReadKind.TimedOut, (await pending).Kind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Preview_skips_tmux_while_the_preview_mark_is_set()
    {
        await File.WriteAllTextAsync(Marker("hang"), string.Empty, TestContext.Current.CancellationToken);
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;
        var first = Task.Run(() => cli.Read("/s", "%5", token), token);
        await WaitFor("started", token);
        _clock.Advance(_budget + TimeSpan.FromMilliseconds(1));
        _ = await first;
        File.Delete(Marker("started"));

        _clock.Advance(TimeSpan.FromSeconds(5));
        var skipped = cli.Read("/s", "%5", token);
        var askedAgain = File.Exists(Marker("started"));
        File.Delete(Marker("hang"));
        var otherSocket = cli.Read("/other", "%5", token);   // the mark is per socket
        _clock.Advance(TimeSpan.FromSeconds(6));
        var later = cli.Read("/s", "%5", token);

        Assert.Equal(PaneReadKind.TimedOut, skipped.Kind);
        Assert.False(askedAgain);   // inside the 10 s window nothing is spawned
        Assert.Equal(PaneReadKind.Captured, otherSocket.Kind);
        Assert.Equal(PaneReadKind.Captured, later.Kind);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Preview_timeout_never_skips_the_collector_pass()
    {
        await File.WriteAllTextAsync(Marker("hang"), string.Empty, TestContext.Current.CancellationToken);
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;
        var preview = Task.Run(() => cli.Read("/s", "%5", token), token);
        await WaitFor("started", token);
        _clock.Advance(_budget + TimeSpan.FromMilliseconds(1));
        _ = await preview;
        File.Delete(Marker("hang"));

        var view = cli.Read("/s", token);   // the pass, right after the preview timed out

        Assert.True(view.IsSuccess);
        Assert.Equal("%5", view.Value.Panes[0].Pane);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Preview_one_deadline_covers_both_tmux_calls()
    {
        await File.WriteAllTextAsync(Marker("hold-display"), string.Empty, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Marker("hang-capture"), string.Empty, TestContext.Current.CancellationToken);
        var cli = Cli();
        var token = TestContext.Current.CancellationToken;

        var pending = Task.Run(() => cli.Read("/s", "%5", token), token);
        await WaitFor("display-started", token);
        _clock.Advance(_budget - TimeSpan.FromMilliseconds(100));   // the first call used all but 100 ms
        await File.WriteAllTextAsync(Marker("go"), string.Empty, token);
        await WaitFor("capture-started", token);
        _clock.Advance(TimeSpan.FromMilliseconds(200));   // under the budget alone, over it in all

        Assert.Equal(PaneReadKind.TimedOut, (await pending).Kind);
    }

    private async Task WaitFor(string marker, CancellationToken token)
    {
        for (var i = 0; i < 200 && !File.Exists(Marker(marker)); i++)
        {
            await Task.Delay(50, token);
        }

        Assert.True(File.Exists(Marker(marker)), "the stand-in never started");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
