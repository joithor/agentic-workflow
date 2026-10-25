using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// A retry with the same nonce (engine spec rev 8 §5.2, review run 1): after a timeout the Update may have applied, so the
/// claim stays and the retry resends the same Update id without checking again (a protected answer asks for no challenge now; it was single
/// use and bound to the instance version, so a second check would refuse an answer that applied). Only the call that
/// created a claim may give it back.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApplyCommandRetryTests
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('b', 32));

    private readonly FakeTimeProvider _clock = new();
    private readonly TemporalLikeRuntime _instance = new();
    private readonly InMemoryCommandNonces _nonces;
    private readonly ApplyCommandService _service;

    public ApplyCommandRetryTests()
    {
        _nonces = new InMemoryCommandNonces(_clock);
        _service = new ApplyCommandService(_instance, _instance, _instance, _nonces, _instance);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_protected_answer_retried_after_a_timeout_that_did_apply_is_applied_not_SND_WF_013_and_keeps_its_claim()
    {
        var address = _instance.Address;
        var request = new ApplyCommandRequest(address.Value, ApplyCommandKind.Answer, AnswerVariant.Grant, Amount: 1, Nonce: _nonce);
        _instance.LoseNextResponse();

        var first = await _service.Handle(request, TestContext.Current.CancellationToken);
        var retry = await _service.Handle(request, TestContext.Current.CancellationToken);
        var claim = await _nonces.Claim(_nonce, Subject(request), TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-002", first.IsError ? first.Error.Code : "accepted");
        Assert.False(retry.IsError, retry.IsError ? retry.Error.Message : null);
        Assert.Equal("resumed (transition): -> produce", retry.Value.Result.Happened);
        Assert.Equal(1, _instance.Applied);
        Assert.Equal(2, _instance.Gets);   // the retry skipped the check; it read the view once more to learn where an answer goes (a mirroring parent forwards it, P7 D5)
        Assert.Equal([_nonce, _nonce], _instance.Sent);
        Assert.Equal(NonceClaim.Applied, claim.Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_concurrent_second_request_on_one_nonce_does_not_take_the_first_ones_claim_away()
    {
        var request = new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Text, Text: "an answer", Nonce: _nonce);
        var entered = _instance.HoldNext();
        var firstRequest = _service.Handle(request, TestContext.Current.CancellationToken).AsTask();
        await entered;
        _instance.RefuseNext();

        var second = await _service.Handle(request, TestContext.Current.CancellationToken);
        var whileOpen = await _nonces.Claim(_nonce, Subject(request), TestContext.Current.CancellationToken);
        _instance.Release();
        var first = await firstRequest;
        var after = await _nonces.Claim(_nonce, Subject(request), TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-003", second.IsError ? second.Error.Code : "accepted");
        Assert.Equal(NonceClaim.Pending, whileOpen.Value);   // B's refusal left A's claim in place
        Assert.False(first.IsError, first.IsError ? first.Error.Message : null);
        Assert.Equal(NonceClaim.Applied, after.Value);       // and A's Applied landed on it
    }

    [Fact(Timeout = 30_000)]
    public async Task A_refusal_of_a_fresh_claim_gives_the_nonce_back()
    {
        var request = new ApplyCommandRequest(_instance.Address.Value, ApplyCommandKind.Answer, AnswerVariant.Text, Text: "an answer", Nonce: _nonce);
        _instance.RefuseNext();

        var refused = await _service.Handle(request, TestContext.Current.CancellationToken);
        var other = await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-003", refused.IsError ? refused.Error.Code : "accepted");
        Assert.Equal(NonceClaim.Fresh, other.Value);
    }

    private string Subject(ApplyCommandRequest request) => _instance.Address.Value + "\n" + ApplyCommandService.ArgumentsSha(request);
}
