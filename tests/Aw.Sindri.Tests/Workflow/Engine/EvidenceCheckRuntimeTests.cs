using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.Workflow.Wiring;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Review run 1 (I4): 'evidence.check' is a descriptor only in this build. A recipe that guards a transition with it validates, and the engine refuses the transition with the named reason (fail closed, W5).</summary>
public sealed class EvidenceCheckRuntimeTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_transition_guarded_by_the_descriptor_only_evidence_check_validates_but_is_refused_at_run_time()
    {
        var recipe = Minimal([WiringRecipe.Go("produce", "done", when: WiringRecipe.Outcome("done")) with { Evidence = [new EvidenceCheck("checks", EvidencePoint.Head, EvidenceExpectation.Pass)] }]);

        Assert.DoesNotContain(WiringRecipe.CheckBlocks(recipe), static f => f.Rule == "RCP-EVID-001");
        var decision = Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")));

        var refused = Assert.IsType<Refused>(decision);
        var evidence = Assert.Single(refused.Changes[0].Event.Evidence);
        Assert.False(evidence.Held);
        Assert.Equal(["no runtime for evidence checks in this build"], evidence.Reasons);
    }
}
