using System.Text.Json.Serialization;

namespace Aw.Kernel.Tests.TypedIds;

[JsonSerializable(typeof(TypedIdPayload))]
[JsonSerializable(typeof(ItemId))]
[JsonSerializable(typeof(IdMaps))]
[JsonSerializable(typeof(PlainMaps))]
[JsonSerializable(typeof(string))]
internal sealed partial class TypedIdJsonContext : JsonSerializerContext;
