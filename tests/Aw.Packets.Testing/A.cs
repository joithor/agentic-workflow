namespace Aw.Packets.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
internal static class A
{
    /// <summary>A ValidatePacketRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ValidatePacketRequestBuilder ValidatePacketRequest() => new();
}
