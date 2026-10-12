using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow.Registry;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Challenges;

/// <summary>
/// The contract every <see cref="IChallenges"/> honours (engine spec §4.2): a challenge redeems once, within 5 minutes,
/// for its own action typed exactly and an unchanged subject; anything else is the caller's stale error. Run against
/// the fake and the in-process adapter (P2).
/// </summary>
public abstract class ChallengesContract
{
    private static readonly ChallengeAction _approve = ChallengeAction.Approve(RecipeId.From("workflow.step"), 1);
    private static readonly ChallengeSubject _subject = ChallengeSubject.ForRecipe(RegistryData.Hash("step"), 1, pinsDigest: null);

    /// <summary>The clock the challenges read; tests advance it.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>Fresh challenges on <see cref="Clock"/>.</summary>
    /// <returns>The challenges.</returns>
    protected abstract IChallenges Create();

    /// <summary>The action text is the one the human types: 'approve workflow.step 1'.</summary>
    [Fact]
    public void Approve_action_text_names_the_id_and_version() => Expect.Equal("approve workflow.step 1", _approve.Text);

    /// <summary>A challenge answered with its action typed exactly redeems once; the second redeem is stale.</summary>
    [Fact]
    public void Challenge_redeems_once()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        var answer = new ChallengeAnswer(issued.Id, "approve workflow.step 1");

        Expect.Equal(issued, challenges.Redeem(answer, _approve, _subject, WorkflowErrors.RecipeChallengeStale).Value);
        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code, challenges.Redeem(answer, _approve, _subject, WorkflowErrors.RecipeChallengeStale).Error.Code);
    }

    /// <summary>After 5 minutes the challenge is refused with the caller's stale error.</summary>
    [Fact]
    public void Expired_challenge_is_refused()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        Clock.Advance(IssuedChallenge.Lifetime);

        Expect.Equal(WorkflowErrors.RecipeChallengeStale.Code,
            challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).Error.Code);
    }

    /// <summary>Typed text that differs, a request for another action, or a changed subject is refused.</summary>
    [Fact]
    public void Mistyped_other_action_or_changed_subject_is_refused()
    {
        var challenges = Create();
        var other = ChallengeAction.Approve(RecipeId.From("workflow.step"), 2);
        var moved = ChallengeSubject.ForRecipe(RegistryData.Hash("step"), 1, pinsDigest: "sha256:changed");

        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, "approve workflow.step"), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, _approve.Text), other, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
        Expect.True(challenges.Redeem(new ChallengeAnswer(challenges.Issue(_approve, _subject).Id, _approve.Text), _approve, moved, WorkflowErrors.RecipeChallengeStale).IsError);
    }

    /// <summary>The typed text is compared after trimming the whitespace a terminal adds around it.</summary>
    [Fact]
    public void Typed_text_is_compared_after_trimming()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);

        Expect.Equal(issued, challenges.Redeem(new ChallengeAnswer(issued.Id, " approve workflow.step 1 "), _approve, _subject, WorkflowErrors.RecipeChallengeStale).Value);
    }

    /// <summary>Any attempt uses the challenge up: a refused one can't be retried with the right text.</summary>
    [Fact]
    public void A_refused_attempt_uses_the_challenge_up()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        _ = challenges.Redeem(new ChallengeAnswer(issued.Id, "approve workflow.step"), _approve, _subject, WorkflowErrors.RecipeChallengeStale);

        Expect.True(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsError);
    }

    /// <summary>A challenge still redeems one tick before it expires.</summary>
    [Fact]
    public void A_challenge_redeems_just_before_it_expires()
    {
        var challenges = Create();
        var issued = challenges.Issue(_approve, _subject);
        Clock.Advance(IssuedChallenge.Lifetime - TimeSpan.FromTicks(1));

        Expect.True(challenges.Redeem(new ChallengeAnswer(issued.Id, _approve.Text), _approve, _subject, WorkflowErrors.RecipeChallengeStale).IsSuccess);
    }
}
