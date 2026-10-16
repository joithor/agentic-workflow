using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>The decision-key list one save binds as '@keys' (read by SQLite's json_each). Internal to Infrastructure.</summary>
[JsonSerializable(typeof(string[]))]
internal sealed partial class IntentJsonContext : JsonSerializerContext;
