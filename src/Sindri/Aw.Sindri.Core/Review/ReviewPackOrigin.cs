namespace Aw.Sindri.Core.Review;

/// <summary>The phase packet a review result packet reviews, as the packet's packet.json and index.json name it.</summary>
/// <param name="Slug">The review packet's id as aw-packets writes it (lower-case letters, digits and '-').</param>
/// <param name="Repo">'owner/name', from the phase packet's packet.json.</param>
/// <param name="BaseSha">The full base commit sha, from the phase packet's packet.json.</param>
/// <param name="PacketHash">The phase packet's canonical hash: the packet's only parent.</param>
/// <param name="AcceptanceHash">'sha256:' and the hex digest of the phase packet's acceptance.json bytes: the nearest ancestor's acceptance, which index.json's 'extends' carries (the review kind has no acceptance.json of its own).</param>
public sealed record ReviewPackOrigin(string Slug, string Repo, string BaseSha, string PacketHash, string AcceptanceHash);
