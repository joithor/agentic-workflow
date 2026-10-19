using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.evidence' on the block runtime (line spec §7.3): runs <see cref="ProofChecks"/> over the phase packet's files and the facts the prove station gathered. A rule that
/// does not hold is 'incomplete' with the findings and nothing is written. Otherwise the proof pack is written to the instance's run area ('proof-pack/') and machine-frozen through
/// aw-packets (kind proof); 'complete' carries <c language="text">proofPack = {hash, dir}</c>. A freeze aw-packets refuses is 'incomplete' with its failed checks as findings. A run area that
/// cannot be written, a tool that cannot run, or a missing address, repo, checkout or phase file fails the Step.
/// </summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the pack is written.</param>
/// <param name="packets">Freezes the pack.</param>
/// <param name="checkouts">Where the repo is checked out (the packet's base commit is read from it).</param>
internal sealed class ProveEvidenceStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    private const string PackName = "proof-pack";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.evidence");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (EvidenceInputs.Of(context) is not { } inputs)
        {
            return BlockStepResult.Failed("step.prove.evidence needs the instance's address and repo and fact:phasePacket (with its hash and dir) and fact:built (with its head)");
        }

        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return BlockStepResult.Failed($"step.prove.evidence has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var (phase, failure) = await PhaseFiles.Load(files, inputs.Dir, cancellationToken).ConfigureAwait(false);
        if (phase is null)
        {
            return BlockStepResult.Failed($"step.prove.evidence can't load the phase packet {inputs.Dir}: {failure}");
        }

        var facts = new ProofFacts(inputs.Head, phase.Acceptance, phase.Signals, phase.Kind, phase.HoldoutCount, StepFacts.Read(context, "fact:proof"), StepFacts.Read(context, "fact:redTests"), StepFacts.Read(context, "fact:beforeAfter"), StepFacts.Read(context, "fact:holdoutResult"));
        var report = ProofChecks.Run(facts);
        if (!report.Holds)
        {
            return Incomplete(report.Failures.Select(static f => Finding(f.Rule, f.Item, f.Message, f.Fix)));
        }

        var origin = new ProofPackOrigin(phase.Id + "-proof", phase.Repo, phase.BaseSha, inputs.PacketHash, ProofPack.Sha256(phase.AcceptanceText));
        return await Freeze(inputs.Address, ProofPack.Files(facts, report, origin), checkout.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Freeze(string address, IReadOnlyDictionary<string, string> pack, string checkout, CancellationToken cancellationToken)
    {
        foreach (var (name, text) in pack)
        {
            var written = await area.Write(address, $"{PackName}/{name}", text, append: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return BlockStepResult.Failed($"step.prove.evidence can't write the proof pack: {written.Error.Message}");
            }
        }

        var dir = area.PathFor(address, PackName);
        if (dir.IsError)
        {
            return BlockStepResult.Failed($"step.prove.evidence can't place the proof pack: {dir.Error.Message}");
        }

        var frozen = await packets.Freeze(new PacketRef(PacketKind.Proof, dir.Value, Repo: checkout), PacketFreezer.Machine, cancellationToken).ConfigureAwait(false);
        if (frozen.IsError)
        {
            return BlockStepResult.Failed($"step.prove.evidence can't freeze the proof pack with aw-packets: {frozen.Error.Message}");
        }

        return frozen.Value is { Frozen: true, Hash: { } hash }
            ? StepFacts.Done("complete", ("fact:proofPack", new JsonObject { ["hash"] = hash, ["dir"] = dir.Value }))
            : Incomplete(frozen.Value.Failures.Select(static f => Finding(f.Rule, item: null, $"{f.Subject}: {f.Message}", f.Fix ?? "fix the packet and run the proof again")));
    }

    private static BlockStepResult Incomplete(IEnumerable<JsonObject> findings) =>
        StepFacts.Done("incomplete", ("fact:findings", new JsonArray([.. findings])));

    private static JsonObject Finding(string rule, string? item, string message, string fix) =>
        new() { ["rule"] = rule, ["item"] = item, ["message"] = message, ["fix"] = fix, ["severity"] = "fail" };
}
