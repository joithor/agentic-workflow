namespace Aw.Context.Infrastructure.Docs;

/// <summary>A stored document: its path, blob and scrub ruleset.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha it was indexed at.</param>
/// <param name="Ruleset">The scrub ruleset it was cleaned with ('' for a row written before S-2W).</param>
internal sealed record StoredDoc(string Path, string Blob, string Ruleset);
