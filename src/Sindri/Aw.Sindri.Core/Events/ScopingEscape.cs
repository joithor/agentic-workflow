namespace Aw.Sindri.Core.Events;

/// <summary>
/// A later station found a surface or behaviour the approved scope packet missed (scoping spec §3 'SC3'): an escape. Ids only, never text.
/// </summary>
/// <param name="PacketHash">The approved packet's canonical hash the escape is measured against.</param>
/// <param name="Station">The station that found it ('design', 'build', 'ship').</param>
/// <param name="Kind">'surface' or 'behaviour'.</param>
/// <param name="Reference">The id the station gave it in its own packet.</param>
public sealed record ScopingEscape(string PacketHash, string Station, string Kind, string Reference);
