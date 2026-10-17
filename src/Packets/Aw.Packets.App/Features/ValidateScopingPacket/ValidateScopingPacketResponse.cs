using Aw.Packets.Core.Checks;

namespace Aw.Packets.App.Features.ValidateScopingPacket;

/// <summary>The ValidateScopingPacket response, also the '--json' output (schemaVersion 1).</summary>
/// <param name="SchemaVersion">The output schema version.</param>
/// <param name="Kind">The packet kind ('scoping').</param>
/// <param name="Passed">True when no check failed (unchecked ones don't fail it).</param>
/// <param name="Checks">Every check outcome: one record per failure, one per passing or unchecked rule.</param>
internal sealed record ValidateScopingPacketResponse(int SchemaVersion, string Kind, bool Passed, IReadOnlyList<CheckResult> Checks);
