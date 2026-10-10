namespace Aw.Packets.Core.Model;

/// <summary>A parsed packet: the manifest and the three JSON files it links.</summary>
/// <param name="Manifest">packet.json.</param>
/// <param name="Decisions">The decision log.</param>
/// <param name="OutOfScope">The out-of-scope list.</param>
/// <param name="Tasks">The workstreams and tasks.</param>
public sealed record Packet(PacketManifest Manifest, DecisionLog Decisions, OutOfScope OutOfScope, TaskPlan Tasks);
