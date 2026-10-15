using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>One fact's scrubbed body, capped at 256 KiB (API spec section 3.8).</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Type">The fact type.</param>
/// <param name="Body">The scrubbed body: an object for scope-map, packet and payload (also when <paramref name="Truncated"/>: the object loses its tail entries and keeps its shape), a string for text.</param>
/// <param name="Truncated">Whether the body was cut at the cap.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactView(string Key, FactType Type, JsonElement Body, bool Truncated);
