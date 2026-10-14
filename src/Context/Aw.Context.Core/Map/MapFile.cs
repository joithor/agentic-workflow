namespace Aw.Context.Core.Map;

/// <summary>A map file as read: the parsed document and the SHA-256 of its bytes (what the owner confirms).</summary>
/// <param name="Document">The document.</param>
/// <param name="Hash">The content hash, 64 lower-case hex digits.</param>
public sealed record MapFile(MapDocument Document, string Hash);
