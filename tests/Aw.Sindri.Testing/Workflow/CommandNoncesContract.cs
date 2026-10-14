using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="ICommandNonces"/> honours (engine spec rev 8 §5.2): a nonce names one command; the same
/// command again is 'already applied' once applied; another command, or a nonce past its 10 minutes, is SND-WF-009; a
/// released claim consumes nothing.
/// </summary>
public abstract class CommandNoncesContract
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('a', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty table on <paramref name="clock"/>.</summary>
    /// <param name="clock">The clock the 10-minute lifetime is read from.</param>
    /// <returns>The table.</returns>
    protected abstract ICommandNonces Create(FakeTimeProvider clock);

    /// <summary>Issued nonces are well formed and distinct.</summary>
    [Fact]
    public void Issued_nonces_are_distinct()
    {
        var nonces = Create(new FakeTimeProvider());

        var issued = Enumerable.Range(0, 5).Select(_ => nonces.Issue().Value).ToList();

        Expect.Equal(5, issued.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A fresh claim is Fresh; the same command again before Applied is Pending; after Applied it is Applied.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_same_command_again_after_it_applied_is_already_applied()
    {
        var nonces = Create(new FakeTimeProvider());

        var first = await nonces.Claim(_nonce, "subject-1", Token);
        var beforeApplied = await nonces.Claim(_nonce, "subject-1", Token);
        _ = await nonces.Applied(_nonce, "subject-1", Token);
        var again = await nonces.Claim(_nonce, "subject-1", Token);

        Expect.Equal((true, NonceClaim.Fresh), (first.IsSuccess, first.Value));
        Expect.Equal((true, NonceClaim.Pending), (beforeApplied.IsSuccess, beforeApplied.Value));
        Expect.Equal((true, NonceClaim.Applied), (again.IsSuccess, again.Value));
    }

    /// <summary>A nonce claimed for one command refuses another with SND-WF-009.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Another_command_with_a_used_nonce_is_SND_WF_009()
    {
        var nonces = Create(new FakeTimeProvider());
        _ = await nonces.Claim(_nonce, "subject-1", Token);
        _ = await nonces.Applied(_nonce, "subject-1", Token);

        var other = await nonces.Claim(_nonce, "subject-2", Token);

        Expect.Equal("SND-WF-009", other.IsError ? other.Error.Code : "accepted");
    }

    /// <summary>A nonce past its 10 minutes is SND-WF-009, even for the same command.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_nonce_past_ten_minutes_is_SND_WF_009()
    {
        var clock = new FakeTimeProvider();
        var nonces = Create(clock);
        _ = await nonces.Claim(_nonce, "subject-1", Token);

        clock.Advance(ICommandNonces.Lifetime + TimeSpan.FromSeconds(1));
        var late = await nonces.Claim(_nonce, "subject-1", Token);

        Expect.Equal("SND-WF-009", late.IsError ? late.Error.Code : "accepted");
    }

    /// <summary>A released claim consumes nothing: the nonce may name another command.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_released_nonce_may_name_another_command()
    {
        var nonces = Create(new FakeTimeProvider());
        _ = await nonces.Claim(_nonce, "subject-1", Token);

        _ = await nonces.Release(_nonce, Token);
        var other = await nonces.Claim(_nonce, "subject-2", Token);

        Expect.Equal((true, NonceClaim.Fresh), (other.IsSuccess, other.Value));
    }

    /// <summary>An Applied for a claim that is gone (given back, swept) is not lost: the claim is recreated as applied.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Applied_for_a_claim_that_is_gone_recreates_it_as_applied()
    {
        var nonces = Create(new FakeTimeProvider());
        _ = await nonces.Claim(_nonce, "subject-1", Token);
        _ = await nonces.Release(_nonce, Token);

        var applied = await nonces.Applied(_nonce, "subject-1", Token);
        var again = await nonces.Claim(_nonce, "subject-1", Token);
        var other = await nonces.Claim(_nonce, "subject-2", Token);

        Expect.True(applied.IsSuccess);
        Expect.Equal((true, NonceClaim.Applied), (again.IsSuccess, again.Value));
        Expect.Equal("SND-WF-009", other.IsError ? other.Error.Code : "accepted");
    }

    /// <summary>A second request on a pending nonce is Pending too, and a late Release never takes back an applied claim.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_late_Release_never_takes_back_an_applied_claim()
    {
        var nonces = Create(new FakeTimeProvider());
        _ = await nonces.Claim(_nonce, "subject-1", Token);
        var second = await nonces.Claim(_nonce, "subject-1", Token);
        _ = await nonces.Applied(_nonce, "subject-1", Token);
        _ = await nonces.Release(_nonce, Token);
        var third = await nonces.Claim(_nonce, "subject-1", Token);

        Expect.Equal((true, NonceClaim.Pending), (second.IsSuccess, second.Value));
        Expect.Equal((true, NonceClaim.Applied), (third.IsSuccess, third.Value));
    }
}
