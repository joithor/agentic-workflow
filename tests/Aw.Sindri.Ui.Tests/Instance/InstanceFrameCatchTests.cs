using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The frame's answer to a 'workflow.instance-changed' frame: it re-reads its own instance, says whether the view reached the frame's state, and leaves other instances alone.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFrameCatchTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private int _reads;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(InstanceFrameViewModel Frame, string Address, string State)> Opened()
    {
        var golden = GoldenFiles.Node("workflow-instance", "project-build");
        var (address, id, state) = ((string)golden["address"]!, (string)golden["id"]!, (string)golden["state"]!);
        _server.InstanceBody = _ => { _reads++; return null; };
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await frame.Open(_api, address, id, Token);
        _reads = 0;
        return (frame, address, state);
    }

    [Fact]
    public async Task A_frame_for_the_instance_shown_is_read_again_and_caught_up_when_the_state_matches()
    {
        var (frame, address, state) = await Opened();

        var caughtUp = await frame.Catch(_api, new InstanceChange(address, state, 10), Token);

        Assert.True(caughtUp);
        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task A_view_still_in_the_old_state_is_not_caught_up()
    {
        var (frame, address, _) = await Opened();

        var caughtUp = await frame.Catch(_api, new InstanceChange(address, "a-state-the-view-has-not-reached", 10), Token);

        Assert.False(caughtUp);
        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task A_frame_for_another_instance_is_ignored_without_a_read()
    {
        var (frame, _, state) = await Opened();

        var caughtUp = await frame.Catch(_api, new InstanceChange("ITM-OTHER/project/1", state, 10), Token);

        Assert.True(caughtUp);
        Assert.Equal(0, _reads);
    }

    [Fact]
    public async Task No_frame_reads_whatever_is_shown_again()
    {
        var (frame, _, _) = await Opened();

        Assert.True(await frame.Catch(_api, change: null, Token));
        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task With_no_instance_shown_nothing_is_read()
    {
        var frame = new InstanceFrameViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));

        Assert.True(await frame.Catch(_api, new InstanceChange("ITM-1/project/1", "build", 10), Token));
        Assert.True(await frame.Catch(_api, change: null, Token));
        Assert.Equal(0, _server.InstancesCalls);
    }
}
