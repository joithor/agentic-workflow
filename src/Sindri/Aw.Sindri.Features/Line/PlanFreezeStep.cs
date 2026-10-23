using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.plan.freeze', on the block runtime (line spec §6.1, LC-5): after the owner's approve, confirms the design packet and the plan packet are the ones the evidence checked (their directories
/// still hash to the facts' hashes), re-runs the plan checks, and has aw-packets freeze, in order, the design packet, the plan packet and each phase packet, as 'owner': the Step runs only on stop 2's
/// human-only approve, and a freeze is write-once, so a machine freeze first would never be upgraded. A hash frozen by someone else first, a failed check or a hash that moved is 'mismatch' with the
/// findings (the recipe loops to evidence). 'frozen' writes 'fact:planPacket' (frozen) and 'fact:phasePackets' ([{ phase, id, hash, dir }]). Not done here, for want of a Step-side port: the
/// design.approved and plan.approved ledger rows and LN-CTX1's proposals to the context map.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
/// <param name="holdouts">The holdout store the frozen plan packet's hash seals each phase's set in.</param>
internal sealed class PlanFreezeStep(IPackets packets, IRepoCheckouts checkouts, IHoldoutStore holdouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.freeze");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var planFact = StepFacts.Read(context, "fact:planPacket");
        var design = StepFacts.Read(context, "fact:design");
        var plan = PacketFact.Of(planFact, "planPacket");
        var designPacket = PacketFact.Of(LineJson.Obj(design, "packet"), "design.packet");
        if (plan.IsError || designPacket.IsError || context.Repo is not { } repo)
        {
            return BlockStepResult.Failed($"{Id.Value}: {(plan.IsError ? plan.Error.Message : designPacket.IsError ? designPacket.Error.Message : "the instance has no repo")}");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't find the repo's checkout: {checkout.Error.Message}");
        }

        var recheck = PlanChecks.Run(design, StepFacts.Read(context, "fact:plan"), StepFacts.Read(context, "fact:holdoutSets"), evidence: true).Failures;
        return recheck.Count > 0 ? LinePackets.Gaps("mismatch", recheck) : await Freeze(Targets(plan.Value, designPacket.Value, LineJson.Items(planFact, "phases")), checkout.Value, context.Address?.Split('/')[0], cancellationToken).ConfigureAwait(false);
    }

    private static List<Target> Targets(PacketFact plan, PacketFact design, IEnumerable<JsonNode> phases) =>
    [
        new(PacketKind.Design, "design packet", design.Directory, design.Hash, Phase: null),
        new(PacketKind.Plan, "plan packet", plan.Directory, plan.Hash, Phase: null),
        .. phases.Select(static p => new Target(PacketKind.Phase, "phase packet " + LineJson.Str(p, "phase"), LineJson.Str(p, "dir") ?? string.Empty, LineJson.Str(p, "hash"), p)),
    ];

    private async ValueTask<BlockStepResult> Freeze(List<Target> targets, string checkout, string? line, CancellationToken cancellationToken)
    {
        if (await Verify(targets, checkout, cancellationToken).ConfigureAwait(false) is { } stale)
        {
            return stale;
        }

        var frozen = new JsonArray();
        var planHash = string.Empty;
        foreach (var target in targets)
        {
            var (stop, hash) = await FreezeOne(target, checkout, cancellationToken).ConfigureAwait(false);
            if (stop is not null)
            {
                return stop;
            }

            if (hash is null)
            {
                return BlockStepResult.Failed($"{Id.Value}: aw-packets froze the {target.Label} but reported no hash");
            }

            if (target.Phase is { } phase)
            {
                LineJson.Append(frozen, new JsonObject { ["phase"] = LineJson.Str(phase, "phase"), ["id"] = LineJson.Str(phase, "id"), ["hash"] = hash, ["dir"] = target.Directory });
            }
            else if (target.Kind == PacketKind.Plan)
            {
                planHash = hash;
            }
        }

        if (await Seal(line, frozen, planHash, cancellationToken).ConfigureAwait(false) is { } unsealed)
        {
            return unsealed;
        }

        var plan = targets.Find(static t => t.Kind == PacketKind.Plan);
        var planFact = new PacketFact(planHash, plan?.Directory ?? string.Empty).ToJson();
        planFact["frozen"] = true;
        return StepFacts.Done("frozen", ("fact:planPacket", planFact), ("fact:phasePackets", frozen));
    }

    // The hidden set of each phase is sealed to the plan packet just frozen (spec §9.1). A phase with no set in the store (facts that name a set the author never delivered) is left unsealed:
    // PLN-HOLD-003 already checked the sets by hash, and a missing file is for holdout-log and the evaluator to find.
    private async ValueTask<BlockStepResult?> Seal(string? line, JsonArray frozen, string planHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        foreach (var id in frozen.Select(static p => LineJson.Str(p, "id")).OfType<string>())
        {
            var sealedSet = await holdouts.SealWith(line, id, planHash, cancellationToken).ConfigureAwait(false);
            if (sealedSet.IsError && sealedSet.Error.Code != WorkflowErrors.HoldoutSetMissing.Code)
            {
                return BlockStepResult.Failed($"{Id.Value} can't seal the hidden set of phase {id} to the plan packet: {sealedSet.Error.Message}");
            }
        }

        return null;
    }

    // Every packet still hashes to what the evidence checked, before the first freeze: a freeze is write-once, so a mismatch found after one would leave a half-frozen set.
    private async ValueTask<BlockStepResult?> Verify(List<Target> targets, string checkout, CancellationToken cancellationToken)
    {
        foreach (var target in targets.Where(static t => t.Hash is not null))
        {
            var expected = target.Hash ?? string.Empty;
            var computed = await packets.Hash(new PacketRef(target.Kind, target.Directory, checkout), cancellationToken).ConfigureAwait(false);
            if (computed.IsError)
            {
                return BlockStepResult.Failed($"{Id.Value}: aw-packets could not hash the {target.Label}: {computed.Error.Message}");
            }

            if (!string.Equals(computed.Value, expected, StringComparison.Ordinal))
            {
                return LinePackets.Gaps("mismatch", [LinePackets.Finding("SND-LINE-001", $"the {target.Label} hashes to {computed.Value} now, not the {expected} the evidence checked", "Run the evidence again; a packet is never edited.")]);
            }
        }

        return null;
    }

    private async ValueTask<(BlockStepResult? Stop, string? Hash)> FreezeOne(Target target, string checkout, CancellationToken cancellationToken)
    {
        var packet = new PacketRef(target.Kind, target.Directory, checkout);
        var result = await packets.Freeze(packet, PacketFreezer.Owner, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return (BlockStepResult.Failed($"{Id.Value}: aw-packets could not freeze the {target.Label}: {result.Error.Message}"), null);
        }

        return Refused(result.Value, target.Label) is { } finding ? (LinePackets.Gaps("mismatch", [finding]), null) : (null, result.Value.Hash);
    }

    // A freeze that did not happen as asked: a check failed, or someone else froze this hash first so the owner's freeze was not recorded.
    private static LineFinding? Refused(PacketFreeze result, string label)
    {
        if (result.Frozen)
        {
            return result.AlreadyFrozenBy is { } by
                ? LinePackets.Finding("PKT-FREEZE-002", $"the {label} was frozen by {by.ToString().ToLowerInvariant()} first, so the owner's freeze was not recorded", "Change the packet (a new hash) and run the evidence again.")
                : null;
        }

        var first = result.Failures.Count > 0 ? result.Failures[0] : null;
        return LinePackets.Finding(first?.Rule ?? "PKT-FREEZE-001", $"the {label} failed its checks at the freeze: {first?.Message ?? "no reason given"}", first?.Fix ?? "Fix the packet and run the evidence again.");
    }

    private sealed record Target(PacketKind Kind, string Label, string Directory, string? Hash, JsonNode? Phase);
}
