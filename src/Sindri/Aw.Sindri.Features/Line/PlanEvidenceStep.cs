using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.plan.evidence', on the block runtime (line spec §6.1): reloads the frozen scope packet (verified), runs the plan checks at the evidence stage and the design checks again
/// over the design the plan intake loaded from its packet, assembles the plan packet in 'plan-packet' and one packet per phase in 'phase-packets/&lt;phase&gt;' (<see cref="PlanPacket"/>,
/// <see cref="PhasePackets"/>), and has aw-packets validate each against the repo's checkout. 'complete' writes 'fact:planPacket' ({ hash, dir, phases[{ phase, id, dir }] }, the phase packets
/// unfrozen); 'incomplete' writes the findings.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="files">The scope packet's files.</param>
/// <param name="area">The instance's run area.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
internal sealed class PlanEvidenceStep(IPackets packets, IPacketFiles files, IRunArea area, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.evidence");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Address is not { } address || LinePackets.RepoSlug(context) is not { } slug || context.Repo is not { } repo
            || StepFacts.Read(context, "fact:design") is not { } design || StepFacts.Read(context, "fact:plan") is not { } plan)
        {
            return BlockStepResult.Failed($"{Id.Value} needs the instance's address and repo and fact:design and fact:plan");
        }

        var scope = await LineScope.Load(packets, files, StepFacts.Read(context, "fact:scopePacket"), cancellationToken).ConfigureAwait(false);
        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError || checkout.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: {(scope.IsError ? scope.Error : checkout.Error).Message}");
        }

        var holdout = StepFacts.Read(context, "fact:holdoutSets");
        var found = DesignChecks.Run(scope.Value, design, LineJson.Arr(design, "lensRounds"), LineJson.Arr(design, "accepted"), evidence: true).Failures
            .Concat(PlanChecks.Run(design, plan, holdout, evidence: true).Failures).ToList();
        if (found.Count > 0)
        {
            return LinePackets.Gaps("incomplete", found);
        }

        var seal = new PlanSeal(packets, area, (address, checkout.Value, slug));
        return await seal.Run(scope.Value, design, plan, (holdout, StepFacts.Read(context, "fact:answerLog")), cancellationToken).ConfigureAwait(false);
    }
}
