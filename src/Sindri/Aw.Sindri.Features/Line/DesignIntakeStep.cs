using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.design.intake', on the block runtime (line spec §4.1): verifies the frozen scope packet (its directory hashes to the hash the fact names: SND-LINE-001 otherwise), loads it
/// into 'fact:scope' (<see cref="LineScope"/>) and writes the eight round-1 seeds. 'fresh', or 'revise' when 'fact:designRevision' holds decisions or notes (the first pass holds
/// text, which is not a revision); a revision seeds 'fact:answers' with it, for the apply Step, and loads the previous design packet ('fact:priorDesignPacket', verified as the
/// scope packet is, SND-LINE-001) into 'fact:design', so apply edits that design; a revision with no previous packet fails, never seeding an empty design. It resolves the style pack and the
/// design lens pack the profile chooses (line spec §5) and pins their ids and hashes in 'fact:packs' ({ style: { id, hash }, lenses: { id, hash } }); a pack that does not resolve fails the
/// Step (SND-PACK-001).
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="files">The scope packet's files.</param>
/// <param name="styles">The style packs.</param>
/// <param name="lensPacks">The lens packs.</param>
internal sealed class DesignIntakeStep(IPackets packets, IPacketFiles files, IStylePacks styles, ILensPacks lensPacks) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.design.intake");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var scope = await LineScope.Load(packets, files, StepFacts.Read(context, "fact:scopePacket"), cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: {scope.Error.Message}");
        }

        if (string.IsNullOrWhiteSpace(LineJson.Str(scope.Value, "baseSha")))
        {
            return BlockStepResult.Failed($"{Id.Value}: SND-LINE-001: the scope packet in {LineJson.Str(scope.Value, "dir")} has no baseSha in its scope-map.json, so the design has no commit to stand on (fix: re-run scoping; the evidence step stamps baseSha from the repo checkout, and a packet is never edited)");
        }

        var packs = await Packs(context, cancellationToken).ConfigureAwait(false);
        if (packs.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: SND-PACK-001: {packs.Error.Message}");
        }

        var revision = StepFacts.Read(context, "fact:designRevision");
        var revise = IsRevision(revision);
        var design = revise ? await PriorDesign(context, scope.Value, cancellationToken).ConfigureAwait(false) : new JsonObject();
        return design.IsError
            ? BlockStepResult.Failed($"{Id.Value}: {design.Error.Message}")
            : StepFacts.Done(
                revise ? "revise" : "fresh",
                ("fact:scope", scope.Value),
                ("fact:packs", packs.Value),
                ("fact:design", design.Value),
                ("fact:findings", new JsonArray()),
                ("fact:lensRounds", new JsonArray()),
                ("fact:changedSections", new JsonArray()),
                ("fact:answers", revise && revision is not null ? new JsonArray(revision.DeepClone()) : []),
                ("fact:accepted", new JsonArray()));
    }

    // The packs the profile chooses for this repo, pinned by id and hash.
    private async ValueTask<Result<JsonObject>> Packs(StepContext context, CancellationToken cancellationToken)
    {
        var style = await styles.Resolve(context.Repo, cancellationToken).ConfigureAwait(false);
        if (style.IsError)
        {
            return style.Error;
        }

        var lenses = await lensPacks.Resolve(LensPackKind.Design, context.Repo, cancellationToken).ConfigureAwait(false);
        return lenses.IsError
            ? lenses.Error
            : new JsonObject { ["style"] = StylePin.Entry(style.Value.Id.Value, style.Value.Hash), ["lenses"] = StylePin.Entry(lenses.Value.Id.Value, lenses.Value.Hash) };
    }

    private async ValueTask<Result<JsonObject>> PriorDesign(StepContext context, JsonNode scope, CancellationToken cancellationToken)
    {
        var prior = StepFacts.Read(context, "fact:priorDesignPacket");
        return prior is JsonObject
            ? await LoadedDesign.Load(packets, files, prior, (LineJson.Str(scope, "hash"), "priorDesignPacket"), cancellationToken).ConfigureAwait(false)
            : SindriErrors.InvalidInput.With("a revision has no previous design packet in fact:priorDesignPacket to edit (fix: start the design station with the previous design packet; a revision edits the previous design and never starts from an empty one)");
    }

    // A revision from stop 2 is an object with decisions or notes; the first pass passes a sentence, which is no revision.
    private static bool IsRevision(JsonNode? revision) =>
        LineJson.Items(revision, "decisions").Any() || LineJson.Items(revision, "notes").Any();
}
