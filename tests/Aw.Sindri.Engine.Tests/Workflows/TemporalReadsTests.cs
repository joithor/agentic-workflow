using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The bounded retry of a control-plane read: the transient codes the SDK used to retry are tried again, the others are not.</summary>
public sealed class TemporalReadsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(RpcException.StatusCode.Unavailable)]
    [InlineData(RpcException.StatusCode.ResourceExhausted)]
    [InlineData(RpcException.StatusCode.Internal)]
    [InlineData(RpcException.StatusCode.Unknown)]
    [InlineData(RpcException.StatusCode.Aborted)]
    [InlineData(RpcException.StatusCode.DataLoss)]
    public async Task A_transient_failure_is_tried_again_and_the_read_then_succeeds(RpcException.StatusCode code)
    {
        var calls = 0;

        var result = await TemporalReads.Run(_ => ++calls < 3 ? throw new RpcException(code, "blip", rawStatus: null) : Task.FromResult(calls), TestContext.Current.CancellationToken);

        Assert.Equal((3, 3), (result, calls));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_that_lasts_is_thrown_after_the_bounded_attempts()
    {
        var calls = 0;

        var thrown = await Assert.ThrowsAsync<RpcException>(() => TemporalReads.Run<int>(_ =>
        {
            calls++;
            throw new RpcException(RpcException.StatusCode.Unavailable, "down", rawStatus: null);
        }, TestContext.Current.CancellationToken));

        Assert.Equal((RpcException.StatusCode.Unavailable, TemporalReads.Pauses.Count + 1), (thrown.Code, calls));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(RpcException.StatusCode.NotFound)]
    [InlineData(RpcException.StatusCode.InvalidArgument)]
    [InlineData(RpcException.StatusCode.DeadlineExceeded)]
    public async Task A_failure_that_a_retry_cannot_fix_is_thrown_at_once(RpcException.StatusCode code)
    {
        var calls = 0;

        _ = await Assert.ThrowsAsync<RpcException>(() => TemporalReads.Run<int>(_ =>
        {
            calls++;
            throw new RpcException(code, "no", rawStatus: null);
        }, TestContext.Current.CancellationToken));

        Assert.Equal(1, calls);
    }
}
