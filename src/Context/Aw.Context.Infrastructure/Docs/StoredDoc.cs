namespace Aw.Context.Infrastructure.Docs;

/// <summary>A stored document: its path and blob.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The blob sha it was indexed at.</param>
internal sealed record StoredDoc(string Path, string Blob);
