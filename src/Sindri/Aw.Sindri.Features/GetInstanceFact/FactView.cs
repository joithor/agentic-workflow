using System.Text.Json;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>One fact's scrubbed body, capped at 256 KiB (API spec section 3.8).</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Type">The fact type.</param>
/// <param name="Body">The scrubbed body: an object for scope-map, packet and payload, a string for text.</param>
/// <param name="Truncated">Whether the body was cut at the cap.</param>
public sealed record FactView(string Key, FactType Type, JsonElement Body, bool Truncated);
