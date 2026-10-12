using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Onyx's wire formats. They are internal to Infrastructure, so no host context can name them; this context is used
/// only through <see cref="OnyxWire.Options"/> (the AwJson profile).
/// </summary>
[JsonSerializable(typeof(OnyxVersionReply))]
internal sealed partial class OnyxWireJsonContext : JsonSerializerContext;
