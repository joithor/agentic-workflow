using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The source-generated context for the two third-party files the collector reads: Claude's session records and the aw:fleet hook's state files.</summary>
[JsonSerializable(typeof(ClaudeRecordFile))]
[JsonSerializable(typeof(FleetStateDocument))]
internal sealed partial class SessionsJsonContext : JsonSerializerContext;
