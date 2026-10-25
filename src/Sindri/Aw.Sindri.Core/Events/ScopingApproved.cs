namespace Aw.Sindri.Core.Events;

/// <summary>
/// The owner approved a scope packet (scoping spec §3 'SC3', the outcome metric). Ids and counts only, never text: the packet holds the text.
/// </summary>
/// <param name="PacketHash">The approved packet's canonical hash ('sha256:…').</param>
/// <param name="Instance">The scoping instance's address.</param>
/// <param name="Surfaces">The scope map's surface ids ('S1').</param>
/// <param name="AcceptanceItems">The acceptance list's item ids ('AC-1').</param>
/// <param name="Rounds">How many draft rounds the packet took.</param>
/// <param name="Rescopes">How many times the scope moved past its anchors and the lenses ran again.</param>
public sealed record ScopingApproved(string PacketHash, string Instance, IReadOnlyList<string> Surfaces, IReadOnlyList<string> AcceptanceItems, int Rounds, int Rescopes);
