using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The adapter's own JSON context for its internal ledger payloads (used only through AwJson.CreateOptions).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkflowEventPayload))]
[JsonSerializable(typeof(ItemFact))]
internal sealed partial class WorkflowsJsonContext : JsonSerializerContext;
