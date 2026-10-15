using System.Text.Json.Serialization;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The proposal payload stored in map_override.payload. Internal to Infrastructure, used only through <see cref="GraphPayload"/>.</summary>
[JsonSerializable(typeof(ProposalPayload))]
internal sealed partial class GraphJsonContext : JsonSerializerContext;
