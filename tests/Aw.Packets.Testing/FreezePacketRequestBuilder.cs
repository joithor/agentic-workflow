using Aw.Packets.App.Features.FreezePacket;

namespace Aw.Packets.Testing;

/// <summary>Builds a FreezePacketRequest with deterministic defaults.</summary>
internal sealed class FreezePacketRequestBuilder
{
    private readonly FreezePacketRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public FreezePacketRequest Build() => _request;
}
