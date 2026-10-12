using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(ActionIdHolder))]
[JsonSerializable(typeof(CommandIdHolder))]
[JsonSerializable(typeof(CorrelationIdHolder))]
[JsonSerializable(typeof(RepoNameHolder))]
internal sealed partial class IdsJsonContext : JsonSerializerContext;
