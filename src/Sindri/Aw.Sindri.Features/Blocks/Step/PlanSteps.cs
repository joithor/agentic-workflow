using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The plan station's Steps (line spec §6, Appendix C.2; LN-Y1): descriptors only, desk-capable. The two Steps that decide
/// with the owner (questions go to 'step.interview.answer' and 'step.interview.record', which scoping already registers) are
/// not repeated here; 'step.plan.review' is the after-design stop, human-only.
/// </summary>
internal static class PlanSteps
{
    private static readonly string[] _loaded = ["loaded"];

    /// <summary>
    /// 'step.plan.intake': loads the candidate design and frozen scope packets; 'loaded' with the round-1 seeds and 'fact:anchors', the anchor
    /// keys of the design's covered surfaces (the surfaces the design's coverage table names, resolved to map anchors through the frozen scope packet's
    /// scope map), which the planner's 'input.context.code' reads (LN-CTX2).
    /// </summary>
    public static BlockDescriptor Intake { get; } = BlockStep("step.plan.intake", Capability.None)
        .Reads("fact:scopePacket", "fact:designPacket").Reports("loaded")
        .Writes(Seeds());

    /// <summary>'step.plan.graph': the phase graph; 'drafted'.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Graph { get; } = AgentStep("step.plan.graph", Capability.Tools)
        .Reads("fact:scopePacket", "fact:design", "fact:plan", "fact:findings", "fact:answerLog").Reports("drafted")
        .Writes(On("fact:plan", "drafted"));

    /// <summary>'step.plan.check': the plan checks and aw-packets validate; 'pass' or 'gaps' with findings.</summary>
    public static BlockDescriptor Check { get; } = BlockStep("step.plan.check", Capability.Tools)
        .Reads("fact:scopePacket", "fact:design", "fact:plan").Reports("pass", "gaps").Writes(On("fact:findings", "gaps"));

    /// <summary>'step.plan.exec-review': each phase as its builder (four answers); 'executable' or 'objections' with findings.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor ExecReview { get; } = AgentStep("step.plan.exec-review", Capability.Tools)
        .Reads("fact:design", "fact:plan").Reports("executable", "objections").Writes(On("fact:findings", "objections"));

    /// <summary>'step.plan.holdout': the hidden-scenario author writes holdout-sets.json in its run folder, never into a fact or packet (PKT-HOLD-002); 'written' with fact:holdoutDraft naming that folder.</summary>
    public static BlockDescriptor Holdout { get; } = AgentStep("step.plan.holdout", Capability.Tools | Capability.WriteScope)
        .Reads("fact:design", "fact:plan").Reports("written").Writes(On("fact:holdoutDraft", "written"));

    /// <summary>'step.plan.holdout-store': moves the author's scenarios into the holdout store, adds the canary and reports the sets (ids, counts and hashes, never text); 'stored', or 'invalid' with findings.</summary>
    public static BlockDescriptor HoldoutStore { get; } = BlockStep("step.plan.holdout-store", Capability.Tools)
        .Reads("fact:scopePacket", "fact:plan", "fact:holdoutDraft").Reports("stored", "invalid")
        .Writes(On("fact:holdoutSets", "stored"), On("fact:findings", "invalid"));

    /// <summary>'step.plan.evidence': checks the design and plan evidence and assembles the candidate plan packet; 'complete' or 'incomplete' with findings.</summary>
    public static BlockDescriptor Evidence { get; } = BlockStep("step.plan.evidence", Capability.Tools)
        .Reads("fact:scopePacket", "fact:design", "fact:plan", "fact:holdoutSets", "fact:findings", "fact:answerLog").Reports("complete", "incomplete")
        .Writes(On("fact:planPacket", "complete"), On("fact:recommendation"), On("fact:findings", "incomplete"));

    /// <summary>'step.plan.questions': turns the open decisions into the stop's questions; 'asked' or 'none'.</summary>
    public static BlockDescriptor Questions { get; } = BlockStep("step.plan.questions", Capability.None)
        .Reads("fact:design", "fact:answers", "fact:answerLog").Reports("asked", "none").Writes(On("fact:questions", "asked"));

    /// <summary>'step.plan.settle': compares the answers with the design's assumptions; 'assumed', 'changed' (a design revision) or 'replan'.</summary>
    public static BlockDescriptor Settle { get; } = BlockStep("step.plan.settle", Capability.None)
        .Reads("fact:design", "fact:answers", "fact:answerLog").Reports("assumed", "changed", "replan").Writes(On("fact:designRevision", "changed"));

    /// <summary>'step.plan.review': the after-design stop, human-only: approve, revise the design, revise the plan, or drop.</summary>
    public static BlockDescriptor Review { get; } = HumanDeskStep("step.plan.review", Capability.None)
        .Reads("fact:planPacket", "fact:design").Reports("approve", "reviseDesign", "revisePlan", "drop")
        .Writes(On("fact:answers", "reviseDesign", "revisePlan"));

    /// <summary>'step.plan.freeze': re-checks and freezes the design, plan and phase packets together; 'frozen' or 'mismatch'.</summary>
    public static BlockDescriptor Freeze { get; } = BlockStep("step.plan.freeze", Capability.Tools)
        .Reads("fact:planPacket", "fact:design", "fact:plan", "fact:holdoutSets").Reports("frozen", "mismatch")
        .Writes(On("fact:planPacket", "frozen"), On("fact:phasePackets", "frozen"), On("fact:findings", "mismatch"));

    private static OutputSpec[] Seeds() =>
    [
        On("fact:design", _loaded), On("fact:plan", _loaded), On("fact:findings", _loaded), On("fact:questions", _loaded),
        On("fact:answers", _loaded), On("fact:answerLog", _loaded), On("fact:holdoutSets", _loaded), On("fact:anchors", _loaded),
    ];
}
