namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The outcome of reading a line packet's packet.json, kept so it is parsed once.</summary>
/// <param name="Manifest">The manifest view; null when packet.json is missing or doesn't match the kind's schema.</param>
/// <param name="Problem">Why it doesn't match, in terms of the file and its fields; null when it matches or is missing.</param>
internal sealed record ManifestRead(LineManifest? Manifest, string? Problem);
