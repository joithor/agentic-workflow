using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>The lock's own JSON context for a third-party wire format whose record is internal (used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(TsHolderRecord))]
internal sealed partial class HolderJsonContext : JsonSerializerContext;
