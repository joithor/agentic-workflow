using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.docs': the repo-docs globs.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DocsSection(IReadOnlyList<string>? Globs = null);
