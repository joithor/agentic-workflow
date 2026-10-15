using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One assertion over a JSON document: the value at a JSON Pointer (RFC 6901) equals, exists or matches. Exactly one of the three is given.</summary>
/// <param name="Location">The JSON Pointer ("" is the whole document).</param>
/// <param name="EqualTo">The value it must equal ('equals' in the script); a default (undefined) element means not given.</param>
/// <param name="Exists">Whether the pointer must resolve (true) or not (false).</param>
/// <param name="Matches">A .NET regular expression the string value, or another scalar's JSON text, must match.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PointerCheck(
    [property: JsonPropertyName("pointer")] string Location,
    [property: JsonPropertyName("equals")] JsonElement EqualTo = default,
    bool? Exists = null,
    string? Matches = null);
