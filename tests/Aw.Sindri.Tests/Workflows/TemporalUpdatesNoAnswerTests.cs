using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What an Update that ran out of patience means (review run 4, qa-r4-1): a frontend that fails its health check, or whose
/// probe throws, is SND-API-001 with the nonce to resend; one that answers means no worker took the command (SND-API-002).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class TemporalUpdatesNoAnswerTests
{
    private const string Target = "127.0.0.1:7233";
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('a', 32));
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/step/1");

    [Fact(Timeout = 30_000)]
    public async Task A_health_probe_that_says_false_is_down_with_the_nonce_to_resend()
    {
        var error = await TemporalUpdates.NoAnswer(Target, static _ => Task.FromResult(false), _address, _nonce, TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-001", error.Code);
        Assert.Contains($"--nonce {_nonce.Value}", error.Message, StringComparison.Ordinal);
        Assert.Contains(Target, error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_health_probe_that_throws_an_rpc_failure_is_down_with_the_nonce_to_resend()
    {
        var error = await TemporalUpdates.NoAnswer(Target, static _ => throw new RpcException(RpcException.StatusCode.Unavailable, "connection refused", rawStatus: null), _address, _nonce, TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-001", error.Code);
        Assert.Contains($"--nonce {_nonce.Value}", error.Message, StringComparison.Ordinal);
        Assert.Contains("connection refused", error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_frontend_that_answers_means_no_worker_took_the_command()
    {
        var error = await TemporalUpdates.NoAnswer(Target, static _ => Task.FromResult(true), _address, _nonce, TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-002", error.Code);
        Assert.Contains($"--nonce {_nonce.Value}", error.Message, StringComparison.Ordinal);
    }
}
