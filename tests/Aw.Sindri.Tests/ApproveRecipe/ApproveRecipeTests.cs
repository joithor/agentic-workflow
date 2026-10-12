using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>Approving a recipe (engine spec §4.1, §4.2, Phase 2's signal): a human at a terminal answers the challenge and gets the approval and its preview.</summary>
public sealed class ApproveRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Approving_workflow_step_as_a_human_at_a_terminal_yields_an_approval_with_its_preview()
    {
        await using var registry = new RegistryHarness();
        _ = await registry.H.HoldLease();
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;
        var step = await registry.Register(document);
        var action = ChallengeAction.Approve(step.Id, step.Version);
        var challenge = registry.Challenges.Issue(action, ChallengeSubject.ForRecipe(step.Hash, step.Version, pinsDigest: null));
        var human = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

        var approved = await registry.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(challenge.Id, "approve workflow.step 1")), human);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        var (approval, preview) = (approved.Value.Approval, approved.Value.Preview);
        Assert.Equal((step.Id, 1, step.Hash, ApprovalVia.Desk, human.Actor), (approval.Id, approval.Version, approval.Hash, approval.Via, approval.Approver));
        Assert.Equal((step.Id, 1, step.Hash), (preview.Id, preview.Version, preview.Hash));
        Assert.NotEmpty(preview.Pins.Blocks);
        Assert.Equal(approval.Pins, preview.Pins);
        Assert.Equal(approval, (await registry.Approvals.Latest(step.Id, 1, step.Hash, TestContext.Current.CancellationToken)).Value);
    }
}
