namespace Aw.Packets.Testing;

/// <summary>The builder root, with deterministic defaults (spec §4b.5).</summary>
internal static class A
{
    /// <summary>A packet that passes every implemented check against <see cref="PacketBuilder.BaseFiles"/>.</summary>
    /// <returns>A new builder.</returns>
    public static PacketBuilder Packet() => new();

    /// <summary>A task in workstream 'ws1' writing 'src/**', with one 'test' acceptance example.</summary>
    /// <param name="id">The task id.</param>
    /// <returns>A new builder.</returns>
    public static TaskBuilder Task(string id) => new(id);

    /// <summary>A scope packet's files that pass every scoping check.</summary>
    /// <returns>A new builder.</returns>
    public static ScopingPacketBuilder ScopingPacket() => new();
}
