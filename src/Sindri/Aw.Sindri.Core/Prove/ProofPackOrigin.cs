namespace Aw.Sindri.Core.Prove;

/// <summary>The phase packet a proof pack proves, as the pack's packet.json and index.json name it.</summary>
/// <param name="Slug">The proof packet's id as aw-packets writes it (lower-case letters, digits and '-').</param>
/// <param name="Repo">'owner/name', from the phase packet's packet.json.</param>
/// <param name="BaseSha">The full base commit sha, from the phase packet's packet.json.</param>
/// <param name="PacketHash">The phase packet's canonical hash: the pack's only parent.</param>
/// <param name="AcceptanceHash">'sha256:' and the hex digest of the phase packet's acceptance.json bytes: the hash the pack's acceptance extends.</param>
public sealed record ProofPackOrigin(string Slug, string Repo, string BaseSha, string PacketHash, string AcceptanceHash);
