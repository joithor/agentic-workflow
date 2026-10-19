using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// One file of the instance's candidate scope packet by hash (scoping spec API9). The hash must be the instance's current
/// fact:scopePacket; the packet directory is hashed again on each read, since a revise rewrites it in place, and a hash that no
/// longer matches is SND-SCOPING-007. The name is one the packet lists, never a path. An unknown instance, hash or name, and another
/// repo's session, answer SND-ITEM-404. The Host fences the text for a session caller.
/// </summary>
/// <param name="instances">Opens the instance for the caller.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="packets">The candidate packet's directory.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
internal sealed class GetPacketFileService(ScopingInstances instances, IFactContents contents, IScopePacketStore packets, ITextScrubber scrubber)
    : IService<GetPacketFileRequest, PacketFile>
{
    public async ValueTask<Result<PacketFile>> Handle(GetPacketFileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var view = await instances.Open(request.Instance, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var current = await ScopingFactText.Read(view.Value, "scopePacket", contents, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        if (!ScopePacketFiles.Names.Contains(request.Name, StringComparer.Ordinal) || !string.Equals(ScopeHash.Of(current.Value), request.Hash, StringComparison.Ordinal))
        {
            return NotFound(request);
        }

        var computed = await packets.Hash(request.Instance, cancellationToken).ConfigureAwait(false);
        if (computed.IsError)
        {
            return computed.Error;
        }

        if (!string.Equals(computed.Value, request.Hash, StringComparison.Ordinal))
        {
            return ScopingErrors.PacketChanged.With($"the packet at {request.Hash} now hashes to {computed.Value} (fix: reopen it from the instance's current packet)");
        }

        var text = await packets.ReadFile(request.Instance, request.Name, cancellationToken).ConfigureAwait(false);
        return text.IsError ? text.Error : new PacketFile(request.Name, scrubber.Scrub(text.Value), ScopePacketFiles.Format(request.Name));
    }

    private static Error NotFound(GetPacketFileRequest request) =>
        SindriErrors.ItemNotFound.With($"No packet file {request.Name} at {request.Hash} on workflow instance {request.Instance.Value} in this repo.");
}
