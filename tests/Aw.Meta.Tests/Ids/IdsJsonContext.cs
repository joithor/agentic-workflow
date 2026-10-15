using System.Text.Json.Serialization;

namespace Aw.Meta.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(MessageIdHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
