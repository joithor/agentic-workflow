using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>This adapter's own JSON context for Onyx's requests and replies, whose records are internal (used only through AwJson.CreateOptions).</summary>
[JsonSerializable(typeof(OnyxVersionWire))]
[JsonSerializable(typeof(OnyxPatWire))]
[JsonSerializable(typeof(OnyxPatInfoWire[]))]
[JsonSerializable(typeof(OnyxPatInfoWire[]))]
[JsonSerializable(typeof(OnyxMeWire))]
[JsonSerializable(typeof(OnyxRegisterRequestWire))]
[JsonSerializable(typeof(OnyxUserEmailWire))]
[JsonSerializable(typeof(OnyxResetWire))]
[JsonSerializable(typeof(OnyxAdminAccessWire))]
[JsonSerializable(typeof(OnyxPatRequestWire))]
internal sealed partial class OnyxHealthJsonContext : JsonSerializerContext;
