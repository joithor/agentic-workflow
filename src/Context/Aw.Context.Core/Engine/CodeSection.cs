using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The profile's 'context.code' section as written (S-CL): the globs and the deadline that steer the code lens's lexical search.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CodeSection(IReadOnlyList<string?>? SchemaGlobs = null, IReadOnlyList<string?>? TestGlobs = null, string? Deadline = null);
