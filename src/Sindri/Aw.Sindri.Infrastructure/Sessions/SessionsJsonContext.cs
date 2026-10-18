using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The source-generated context for the three third-party files the collector reads: Claude's session records, the aw:fleet hook's state files and a Meta team.json.</summary>
[JsonSerializable(typeof(ClaudeRecordFile))]
[JsonSerializable(typeof(FleetStateDocument))]
[JsonSerializable(typeof(TeamFile))]
internal sealed partial class SessionsJsonContext : JsonSerializerContext;
