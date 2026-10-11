using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class SingleLaunchTests
{
    private readonly FakeInstanceChannel _channel = new();

    [Fact]
    public void The_first_launch_runs_the_app()
    {
        var first = new FakeSingleInstance(_channel);

        Assert.Equal(7, SingleLaunch.Run(first, [], () => 7));
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
        });

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
}
