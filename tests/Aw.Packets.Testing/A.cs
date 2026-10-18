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

    /// <summary>A sizes file with the line spec's Q5 limits (XS 3 files and 2 items, S 8 and 4, M 20 and 8, L 40 and 16, XL 80 and 32) and a budget that grows with the size.</summary>
    /// <returns>The sizes.</returns>
    public static Core.Model.SizesFile Sizes() => SizesBuilder.Default();

    /// <summary>A scope packet's files that pass every scoping check.</summary>
    /// <returns>A new builder.</returns>
    public static ScopingPacketBuilder ScopingPacket() => new();

    /// <summary>A line packet's files (design, plan, phase, proof, review or handoff) that pass every check of the kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The packet id; the kind's default when null.</param>
    /// <returns>A new builder.</returns>
    public static LinePacketBuilder LinePacket(Core.Kinds.PacketKind kind, string? id = null) => new(kind, id);
}
