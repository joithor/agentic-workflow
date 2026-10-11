using System.Text.Json.Serialization;

namespace Aw.Evidence.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(RunIdHolder))]
[JsonSerializable(typeof(RunInstanceIdHolder))]
[JsonSerializable(typeof(TestIdHolder))]
[JsonSerializable(typeof(AttachmentIdHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
