using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.docs:' block (PB spec PB6, §7): whether docs sync runs and what counts as a doc.</summary>
/// <param name="Sync">'on' or 'off' (default on); 'off' opts the repo out and the babysitter skips docs sync.</param>
/// <param name="Globs">The paths that count as docs (default 'docs/**' and '*.md').</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocsSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sync = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Globs = null);
