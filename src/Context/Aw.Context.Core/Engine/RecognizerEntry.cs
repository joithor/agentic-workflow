using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>One 'context.extract.recognizers' entry as written (S-SK): every field optional here so a missing one is named by key path.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record RecognizerEntry(string? Kind = null, string? Glob = null, string? Pattern = null, string? KeyFrom = null);
