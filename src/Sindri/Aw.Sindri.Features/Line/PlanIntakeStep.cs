using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.plan.intake', on the block runtime (line spec §6.1): verifies the frozen scope packet and the candidate design packet by recomputing their hashes from their directories (SND-LINE-001
/// on a mismatch, or when the design packet's index does not name the scope packet as its parent) and writes the seven seeds; 'fact:design' is the design the packet's files make
/// (<see cref="DesignPacket.FromFiles"/>, with the packet's hash and directory under 'packet'). The port has no verify, so a freeze record is not looked up.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="files">The packets' files.</param>
internal sealed class PlanIntakeStep(IPackets packets, IPacketFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.intake");

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

        var design = await LoadDesign(StepFacts.Read(context, "fact:designPacket"), LineJson.Str(scope.Value, "hash"), cancellationToken).ConfigureAwait(false);
        return design.IsError
            ? BlockStepResult.Failed($"{Id.Value}: {design.Error.Message}")
            : StepFacts.Done(
                "loaded",
                ("fact:design", design.Value),
                ("fact:plan", new JsonObject()),
                ("fact:findings", new JsonArray()),
                ("fact:questions", new JsonArray()),
                ("fact:answers", new JsonArray()),
                ("fact:answerLog", new JsonArray()),
                ("fact:holdoutSets", new JsonArray()));
    }

    private async ValueTask<Result<JsonObject>> LoadDesign(JsonNode? fact, string? scopeHash, CancellationToken cancellationToken)
    {
        var packet = PacketFact.Of(fact, "designPacket");
        if (packet.IsError)
        {
            return packet.Error;
        }

        var verified = await LinePackets.Verify(packets, PacketKind.Design, packet.Value, cancellationToken).ConfigureAwait(false);
        if (verified.IsError)
        {
            return verified.Error;
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in DesignPacket.FileNames.Append("index.json"))
        {
            var read = await files.Read(packet.Value.Directory, name, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            texts[name] = read.Value;
        }

        return NamesParent(texts["index.json"], scopeHash)
            ? DesignPacket.FromFiles(texts, packet.Value)
            : SindriErrors.InvalidInput.With($"SND-LINE-001: the design packet's index.json does not name the scope packet {scopeHash} as a parent (fix: re-run the design station on the frozen scope packet)");
    }

    private static bool NamesParent(string index, string? scopeHash)
    {
        try
        {
            return scopeHash is not null && LineJson.Strings(JsonNode.Parse(index), "parents").Contains(scopeHash, StringComparer.Ordinal);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;   // boundary: a packet file is text a model wrote
        }
    }
}
