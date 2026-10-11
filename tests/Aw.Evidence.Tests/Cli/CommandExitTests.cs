using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Host;
using Aw.Evidence.Host.Commands;
using Aw.Evidence.Testing;
using Aw.Evidence.Tests.Infrastructure;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Cli;

/// <summary>
/// The exit-code table end to end in process: the real composition and command table, with the browser, locks, stack
/// probe, settings and installer swapped for fakes, and Console's writers captured.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(ConsoleCapture.Name)]
public sealed class CommandExitTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly FakeBrowser _browser = new();
    private readonly InMemoryMachineLocks _locks = new();
    private readonly FakeStackProbe _probe = new() { Title = "App" };
    private readonly FakeEvidenceSettings _settings = new() { AppTitle = "App" };
    private readonly FakeBrowserInstaller _installer = new();

    [Fact]
    public async Task A_run_whose_step_broke_exits_2_and_still_prints_the_summary()
    {
        var (exit, output, _) = await Run("run", Script(), _root.Combine("run-1"));   // the page has no 'open'

        Assert.Equal(CommandOutput.CheckFailed, exit);
        Assert.Contains("\"status\":\"broken\"", output, StringComparison.Ordinal);
        Assert.True(File.Exists(_root.Combine("run-1/summary.json")));
    }

    [Fact]
    public async Task A_run_whose_browser_fails_to_start_exits_4_with_no_summary()
    {
        _browser.FailsToStart = true;

        var (exit, output, error) = await Run("run", Script(), _root.Combine("run-1"));

        Assert.Equal((CommandOutput.RunErrored, ""), (exit, output));
        Assert.StartsWith("ui-evidence: AW-EVIDENCE-013 ", error, StringComparison.Ordinal);
        Assert.False(File.Exists(_root.Combine("run-1/summary.json")));
    }

    [Fact]
    public async Task An_unexpected_exception_exits_4_with_a_one_line_error_not_a_crash()
    {
        _browser.PageFor = static _ => new FakePage { ScreenshotThrows = new InvalidOperationException("the browser crashed\nat frame 3") };

        var (exit, output, error) = await Run("run", Script(), _root.Combine("run-1"));

        Assert.Equal((CommandOutput.RunErrored, ""), (exit, output));
        Assert.Equal("ui-evidence: AW-EVIDENCE-015 the run failed unexpectedly: the browser crashed\n", error.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.False(_locks.IsHeld(LockKind.QaStack));
    }

    [Fact]
    public async Task A_cancellation_no_signal_caused_exits_4_with_a_one_line_error_not_a_crash()
    {
        _browser.PageFor = static _ => new FakePage { ScreenshotThrows = new TaskCanceledException("a library timed out") };

        var (exit, output, error) = await Run("run", Script(), _root.Combine("run-1"));

        Assert.Equal((CommandOutput.RunErrored, ""), (exit, output));
        Assert.Equal("ui-evidence: AW-EVIDENCE-015 the run failed unexpectedly: a library timed out\n", error.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_stderr_message_stays_one_line_whatever_text_it_carries()
    {
        var (exit, _, error) = await Run("frob\nnicate");

        Assert.Equal(CommandOutput.Refused, exit);
        _ = Assert.Single(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("unknown command 'frob nicate'", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_busy_lock_exits_75()
    {
        _ = _locks.AlwaysBusy.Add(LockKind.HeavyJob);

        var (exit, _, error) = await Run("run", Script(), _root.Combine("run-1"));

        Assert.Equal(CommandOutput.LockBusy, exit);
        Assert.Contains("AW-EVIDENCE-009", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parity_whose_browser_fails_to_start_exits_4()
    {
        _browser.FailsToStart = true;
        var manifest = _root.Combine("manifest.json");
        await File.WriteAllTextAsync(manifest, TestJson.Write(A.Manifest().Build()), TestContext.Current.CancellationToken);

        var (exit, output, _) = await Run("parity", manifest, _root.Combine("parity-1"));

        Assert.Equal((CommandOutput.RunErrored, ""), (exit, output));
    }

    [Fact]
    public async Task An_unhealthy_stack_exits_2_with_a_doctor_line_per_failure()
    {
        _probe.DatabaseReady = false;

        var (exit, output, error) = await Run("doctor");

        Assert.Equal(CommandOutput.CheckFailed, exit);
        Assert.Contains("\"healthy\":false", output, StringComparison.Ordinal);
        Assert.Contains("ui-evidence: doctor: the database is not ready", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_browser_install_exits_4()
    {
        _installer.Fails = true;

        var (exit, output, _) = await Run("install-browser");

        Assert.Equal((CommandOutput.RunErrored, ""), (exit, output));
    }

    public void Dispose() => _root.Dispose();

    private string Script()
    {
        var path = _root.Combine("script.json");
        File.WriteAllText(path, TestJson.Write(A.Script().WithStep(ScriptBuilder.Click("open")).Build()));
        return path;
    }

    private async Task<(int Exit, string Out, string Err)> Run(params string[] args)
    {
        var services = Composition.Compose(
            new ServiceCollection(), name => name switch { "AW_STATE_DIR" => _root.Combine("state"), "AW_LOG_LEVEL" => "none", _ => null }, new FakeTimeProvider());
        _ = services.Replace(ServiceDescriptor.Singleton<IBrowser>(_browser));
        _ = services.Replace(ServiceDescriptor.Singleton<IMachineLocks>(_locks));
        _ = services.Replace(ServiceDescriptor.Singleton<IStackProbe>(_probe));
        _ = services.Replace(ServiceDescriptor.Singleton<IEvidenceSettings>(_settings));
        _ = services.Replace(ServiceDescriptor.Singleton<IBrowserInstaller>(_installer));
        await using var provider = services.BuildServiceProvider();
        var (output, error) = (new StringWriter(), new StringWriter());
        var (stdout, stderr) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exit = await CommandTable.Run(args, provider);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }
}
