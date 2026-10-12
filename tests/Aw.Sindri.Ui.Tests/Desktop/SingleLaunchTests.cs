using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class SingleLaunchTests : IDisposable
{
    private readonly FakeInstanceChannel _channel = new();
    private readonly StringWriter _errors = new();
    private readonly RecordingLogger _log = new();

    public void Dispose() => _errors.Dispose();

    [Fact]
    public void The_first_launch_runs_the_app()
    {
        var first = new FakeSingleInstance(_channel);

        Assert.Equal(7, SingleLaunch.Run(first, [], () => 7, _errors, _log));
    }

    [Fact]
    public async Task A_second_launch_sends_focus_and_exits_0_without_running()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        await using var reader = new InstanceReader(first, TestContext.Current.CancellationToken);
        var ran = false;

        var exit = SingleLaunch.Run(new FakeSingleInstance(_channel), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(0, exit);
        Assert.False(ran);
        Assert.Equal([InstanceMessages.Focus], await reader.Take(1));
    }

    // The running instance holds the claim but doesn't handle the message (a failed start-up, a wedged app): the later
    // launch doesn't report success, and names the process to quit.
    [Fact]
    public async Task A_second_launch_to_an_instance_that_does_not_answer_exits_1_naming_its_process()
    {
        var first = new FakeSingleInstance(_channel, processId: 4242);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        var ran = false;

        var exit = SingleLaunch.Run(new FakeSingleInstance(_channel), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(1, exit);
        Assert.False(ran);
        Assert.Equal("sindri-ui: Sindri app is running but not answering (pid 4242): quit it or kill it" + Environment.NewLine, _errors.ToString());
    }

    // The holder ended between this launch's claim and its message: this launch claims after all and runs the app.
    [Fact]
    public async Task A_second_launch_takes_over_when_the_holder_ends_before_it_answers()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        var second = new EndsHolderOnSend(new FakeSingleInstance(_channel), first);

        Assert.Equal(7, SingleLaunch.Run(second, [], () => 7, _errors, _log));
        Assert.Equal(string.Empty, _errors.ToString());
    }

    [Theory]
    [InlineData(new string[0], InstanceMessages.Focus)]
    [InlineData(new[] { "item/ITM-01JC8" }, "item/ITM-01JC8")]
    [InlineData(new[] { "sindri://item/ITM-01JC8" }, "item/ITM-01JC8")]
    [InlineData(new[] { "--verbose" }, InstanceMessages.Focus)]
    [InlineData(new[] { "item/a", "item/b" }, InstanceMessages.Focus)]
    public void A_later_launch_sends_its_deep_link_or_focus(string[] args, string message) =>
        Assert.Equal(message, SingleLaunch.MessageFor(args));

    // The app ends with 2 when its start-up failed (SindriApp logs the cause); the launch says so on standard error.
    [Fact]
    public void A_failed_start_up_exits_2_with_a_line_on_stderr()
    {
        Assert.Equal(2, SingleLaunch.Run(new FakeSingleInstance(_channel), [], () => 2, _errors, _log));

        Assert.StartsWith("sindri-ui: couldn't start (SND-UI-004)", _errors.ToString(), StringComparison.Ordinal);
    }

    // A failure before the UI starts (the 'ui' directory can't be made: a file sits there) is the documented SND-UI-004:
    // the code and the exception's type in the log, one line on standard error, exit 2. Never the path, which names the
    // user's home, and never a stack trace.
    [Fact]
    public void A_failure_before_the_UI_starts_exits_2_with_SND_UI_004_and_no_path()
    {
        var ran = false;

        var exit = SingleLaunch.Run(new FailsToClaim(new IOException("The file '/Users/someone/.agentic-workflow/sindri-app/ui' already exists.")), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(2, exit);
        Assert.False(ran);
        // Lead (1): true whether or not a log line printed (AW_LOG_LEVEL=none): the cause is named on the line itself.
        Assert.Equal("sindri-ui: couldn't start (SND-UI-004: IOException)" + Environment.NewLine, _errors.ToString());
        var entry = Assert.Single(_log.Entries);
        Assert.Equal("SND-UI-004", entry.EventName);
        Assert.Contains("IOException", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/someone", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
    }

    // The same for a failure in the hand-over to the running instance (a socket it can't reach for a reason other than "nothing listens").
    [Fact]
    public void A_failure_handing_over_exits_2_with_SND_UI_004()
    {
        var exit = SingleLaunch.Run(new FailsToSend(new UnauthorizedAccessException("Access to the path '/Users/someone/ui/instance.sock' is denied.")), [], () => 0, _errors, _log);

        Assert.Equal(2, exit);
        Assert.DoesNotContain("/Users/someone", _errors.ToString(), StringComparison.Ordinal);
        Assert.Equal("SND-UI-004", Assert.Single(_log.Entries).EventName);
    }

    private sealed class FailsToClaim(Exception failure) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromException<bool>(failure);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public IAsyncEnumerable<string> Messages(CancellationToken cancellationToken) => AsyncEnumerable.Empty<string>();

        public int? HolderProcessId() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailsToSend(Exception failure) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => ValueTask.FromException<bool>(failure);

        public IAsyncEnumerable<string> Messages(CancellationToken cancellationToken) => AsyncEnumerable.Empty<string>();

        public int? HolderProcessId() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // A later launch whose message arrives just as the holder ends.
    private sealed class EndsHolderOnSend(ISingleInstance inner, ISingleInstance holder) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => inner.TryClaim(cancellationToken);

        public async ValueTask<bool> Send(string message, CancellationToken cancellationToken)
        {
            await holder.DisposeAsync();
            return await inner.Send(message, cancellationToken);
        }

        public IAsyncEnumerable<string> Messages(CancellationToken cancellationToken) => inner.Messages(cancellationToken);

        public int? HolderProcessId() => inner.HolderProcessId();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
