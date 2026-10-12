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

    public void Dispose() => _errors.Dispose();

    [Fact]
    public void The_first_launch_runs_the_app()
    {
        var first = new FakeSingleInstance(_channel);

        Assert.Equal(7, SingleLaunch.Run(first, [], () => 7, _errors));
    }

    [Fact]
    public async Task A_second_launch_sends_focus_and_exits_0_without_running()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        var ran = false;

        var exit = SingleLaunch.Run(new FakeSingleInstance(_channel), [], () =>
        {
            ran = true;
            return 0;
        }, _errors);

        Assert.Equal(0, exit);
        Assert.False(ran);
        await using var messages = first.Messages(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await messages.MoveNextAsync());
        Assert.Equal(InstanceMessages.Focus, messages.Current);
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
        Assert.Equal(2, SingleLaunch.Run(new FakeSingleInstance(_channel), [], () => 2, _errors));

        Assert.StartsWith("sindri-ui: couldn't start (SND-UI-004)", _errors.ToString(), StringComparison.Ordinal);
    }
}
