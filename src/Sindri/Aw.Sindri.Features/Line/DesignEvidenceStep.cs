using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.design.evidence', on the block runtime (line spec §4.1, §4.3): the design checks at the evidence stage, then the design packet assembled in the run area's 'design-packet'
/// (<see cref="DesignPacket"/>), checked by aw-packets validate against the repo's checkout (PKT-BASE-001 needs the scope's base commit) and hashed. 'complete' writes the candidate
/// 'fact:designPacket' ({ hash, dir }) with the design and lens rounds unchanged; 'incomplete' writes the findings, from the checks or from aw-packets. The escapes the design adds
/// are in the packet's escapes.json (LC-7); no 'line.escape' ledger row is written: a Step has no ledger port.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="area">The instance's run area.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
/// <param name="styles">The style packs, to read the pinned one again (DSN-STYLE-001).</param>
internal sealed class DesignEvidenceStep(IPackets packets, IRunArea area, IRepoCheckouts checkouts, IStylePacks styles) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.design.evidence");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Read(context, "fact:scope") is not { } scope)
        {
            return BlockStepResult.Failed($"{Id.Value} needs fact:scope");
        }

        var design = StepFacts.Read(context, "fact:design");
        var rounds = StepFacts.Read(context, "fact:lensRounds");
        var pinned = StylePin.Read(styles, StepFacts.Read(context, "fact:packs"));
        if (pinned.Failure is { } failure)
        {
            return BlockStepResult.Failed($"{Id.Value}: {failure}");
        }

        var report = DesignChecks.RunStyled(scope, design, (rounds, StepFacts.Read(context, "fact:accepted")), evidence: true, pinned.Pack);
        if (!report.Passed)
        {
            return LinePackets.Gaps("incomplete", report.Failures);
        }

        if (context.Address is not { } address || LinePackets.RepoSlug(context) is not { } slug || context.Repo is not { } repo || design is null)
        {
            return BlockStepResult.Failed($"{Id.Value} needs the instance's address and repo and fact:scope and fact:design");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't find the repo's checkout: {checkout.Error.Message}");
        }

        var files = DesignPacket.Assemble(scope, StepFacts.Read(context, "fact:packs"), design, (rounds, StepFacts.Read(context, "fact:accepted")), slug);
        if (files.IsError)
        {
            return LinePackets.Gaps("incomplete", [LinePackets.Finding("DSN-PKT-001", files.Error.Message, "Complete the design so it can be a packet.")]);
        }

        return await Seal(address, files.Value, checkout.Value, (design, rounds), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Seal(string address, IReadOnlyList<PacketText> files, string checkout, (JsonNode Design, JsonNode? Rounds) facts, CancellationToken cancellationToken)
    {
        var written = await LinePackets.Write(area, address, LinePackets.DesignDirectory, files, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't write the design packet: {written.Error.Message}");
        }

        var packet = new PacketRef(PacketKind.Design, written.Value, checkout);
        var validation = await packets.Validate(packet, cancellationToken).ConfigureAwait(false);
        if (validation.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: aw-packets could not check the design packet: {validation.Error.Message}");
        }

        if (!validation.Value.Passed)
        {
            return LinePackets.Gaps("incomplete", LinePackets.Findings(validation.Value.Failures));
        }

        var hash = await packets.Hash(packet, cancellationToken).ConfigureAwait(false);
        return hash.IsError
            ? BlockStepResult.Failed($"{Id.Value}: aw-packets could not hash the design packet: {hash.Error.Message}")
            : StepFacts.Done("complete", ("fact:designPacket", new PacketFact(hash.Value, written.Value).ToJson()), ("fact:design", facts.Design.DeepClone()), ("fact:lensRounds", facts.Rounds?.DeepClone() ?? new JsonArray()));
    }
}
