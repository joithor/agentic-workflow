using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>A report retried with its nonce after a timeout (engine spec rev 8 §5.2, review run 1): the claim stays and the same Update id is resent.</summary>
[Lane(TestLane.Unit)]
public sealed class CompleteStepRetryTests
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('d', 32));

    private readonly TemporalLikeRuntime _instance = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());

    [Fact(Timeout = 30_000)]
    public async Task A_report_retried_after_a_timeout_that_did_apply_is_applied_and_keeps_its_claim()
    {
        var service = new CompleteStepService(_instance, _instance, new InMemoryFactStore(), _nonces, _instance);
        var request = Report();
        _instance.LoseNextResponse();

        var first = await service.Handle(request, TestContext.Current.CancellationToken);
        var retry = await service.Handle(request, TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-002", first.IsError ? first.Error.Code : "accepted");
        Assert.False(retry.IsError, retry.IsError ? retry.Error.Message : null);
        Assert.Equal(1, _instance.Applied);
        Assert.Equal([_nonce, _nonce], _instance.Sent);
        Assert.Equal(NonceClaim.Applied, (await _nonces.Claim(_nonce, _instance.Address.Value + "\n" + ReportSha(), TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_refused_report_gives_its_fresh_nonce_back()
    {
        var service = new CompleteStepService(_instance, _instance, new InMemoryFactStore(), _nonces, _instance);
        _instance.RefuseNext();

        var refused = await service.Handle(Report(), TestContext.Current.CancellationToken);
        var other = await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-003", refused.IsError ? refused.Error.Code : "accepted");
        Assert.Equal(NonceClaim.Fresh, other.Value);
    }

    private static CompleteStepRequest Report() =>
        new("T-1/step/1", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

    private static string ReportSha() => CompleteStepService.ReportSha(Report(), []);
}
