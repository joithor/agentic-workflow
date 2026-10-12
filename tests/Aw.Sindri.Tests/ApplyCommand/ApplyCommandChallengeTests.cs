using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>The action classes of the commands (engine spec §6.2, W22) and the action text of a protected answer's challenge (§4.2).</summary>
[Lane(TestLane.Unit)]
public sealed class ApplyCommandChallengeTests
{
    [Theory]
    [InlineData(AnswerVariant.Grant)]
    [InlineData(AnswerVariant.GrantTokens)]
    [InlineData(AnswerVariant.Accept)]
    public void Grant_grant_tokens_and_accept_are_protected(AnswerVariant variant) =>
        Assert.True(ApplyCommandChallenge.IsProtected(ApplyCommandKind.Answer, variant));

    [Theory]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Text)]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Resume)]
    [InlineData(ApplyCommandKind.Answer, AnswerVariant.Reject)]
    [InlineData(ApplyCommandKind.Command, null)]
    [InlineData(ApplyCommandKind.Cancel, null)]
    public void Text_resume_reject_commands_and_cancel_are_desk(ApplyCommandKind kind, AnswerVariant? variant) =>
        Assert.False(ApplyCommandChallenge.IsProtected(kind, variant));

    [Fact]
    public void A_protected_answer_names_its_amount_and_the_instance()
    {
        var address = InstanceAddress.From("T-1/step/1");

        Assert.Equal(
            ["grant 3 T-1/step/1", "grant-tokens 100000 T-1/step/1", "accept T-1/step/1"],
            [ApplyCommandChallenge.Action(address, AnswerVariant.Grant, 3).Text, ApplyCommandChallenge.Action(address, AnswerVariant.GrantTokens, 100_000).Text, ApplyCommandChallenge.Action(address, AnswerVariant.Accept, amount: null).Text]);
    }
}
