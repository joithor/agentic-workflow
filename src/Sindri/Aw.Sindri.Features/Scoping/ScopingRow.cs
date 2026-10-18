using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>One row: a line of text the view shows, with its detail lines.</summary>
/// <param name="Key">What the row is about (a rule, an id, a lens, a node key).</param>
/// <param name="Text">The row's text, exactly as shown.</param>
/// <param name="Status">A short status word (pass, fail, warn, not-run, limit, mapped, new, unmapped, unknown), or null.</param>
/// <param name="Lines">Detail lines under the row.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingRow(string Key, string Text, string? Status, IReadOnlyList<string> Lines);
