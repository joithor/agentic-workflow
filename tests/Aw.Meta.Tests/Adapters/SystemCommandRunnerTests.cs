using Aw.Meta.Core.Commands;
using Aw.Meta.Infrastructure.Processes;

namespace Aw.Meta.Tests.Adapters;

public sealed class SystemCommandRunnerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task The_working_directory_and_environment_are_honored()
    {
        var request = new CommandRequest(["sh", "-c", "pwd; printf %s \"$META_TEST\""], TimeSpan.FromSeconds(20))
        {
            Cwd = _dir.Path,
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["META_TEST"] = "value-1" },
        };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(Path.GetFileName(_dir.Path), result.Value.Stdout, StringComparison.Ordinal);
        Assert.EndsWith("value-1", result.Value.Stdout, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Both_streams_are_captured_and_appended_to_the_log()
    {
        var log = Path.Combine(_dir.Path, "out", "step.log");
        var request = new CommandRequest(["sh", "-c", "echo to-out; echo to-err 1>&2"], TimeSpan.FromSeconds(20)) { LogFile = log };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.Contains("to-out", result.Value.Stdout, StringComparison.Ordinal);
        Assert.Contains("to-err", result.Value.Stderr, StringComparison.Ordinal);
        var text = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
        Assert.Contains("to-out", text, StringComparison.Ordinal);
        Assert.Contains("to-err", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cancelling_kills_the_command_and_surfaces_the_cancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var request = new CommandRequest(["sleep", "60"], TimeSpan.FromSeconds(60));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new SystemCommandRunner().Run(request, cancel.Token));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_working_directory_is_a_start_failure()
    {
        var request = new CommandRequest(["true"], TimeSpan.FromSeconds(5)) { Cwd = Path.Combine(_dir.Path, "absent") };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.True(result.IsError);
    }

    public void Dispose() => _dir.Dispose();
}
