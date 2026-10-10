using Aw.Packets.App.Features.ValidatePacket;

namespace Aw.Packets.Testing;

/// <summary>Builds a ValidatePacketRequest with deterministic defaults.</summary>
internal sealed class ValidatePacketRequestBuilder
{
    private readonly ValidatePacketRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ValidatePacketRequest Build() => _request;
}
