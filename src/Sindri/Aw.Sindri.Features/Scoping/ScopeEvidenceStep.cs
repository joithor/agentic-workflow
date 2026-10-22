using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.evidence', on the block runtime (scoping spec §8.2, SC7): <c language="text">ScopingSteps.Evidence</c>'s code behind <see cref="IBlockStep"/>. Every check runs over the facts,
/// and on 'complete' the packet is assembled in the instance's run area as the desk assembles it, so the owner's stop reads files, not a claim. A run area that cannot be written fails the Step.
/// </summary>
/// <param name="area">The instance's run area.</param>
/// <param name="packets">Hashes the assembled packet, which 'complete' records as fact:scopePacket for the owner's approval and the freeze.</param>
internal sealed class ScopeEvidenceStep(IRunArea area, IPackets packets) : ScopingCodeStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.evidence");

    /// <inheritdoc/>
    protected override ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        WriteFiles(area, context, response, cancellationToken);

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> MoreOutputs(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken)
    {
        if (response.Outcome != "complete")
        {
            return Facts([]);
        }

        if (context.Address is not { } address)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With("the Step has no instance address, so there is no scope packet to hash");
        }

        var dir = area.PathFor(address, "scope-packet");
        if (dir.IsError)
        {
            return dir.Error;
        }

        var hash = await packets.Hash(new PacketRef(PacketKind.Scoping, dir.Value), cancellationToken).ConfigureAwait(false);
        return hash.IsError
            ? hash.Error
            : Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["scopePacket"] = new JsonObject { ["hash"] = hash.Value }.ToJsonString() });
    }
}
