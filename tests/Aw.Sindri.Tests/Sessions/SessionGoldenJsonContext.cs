using System.Text.Json.Serialization;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The test-side context for the session goldens.</summary>
[JsonSerializable(typeof(GoldenSnapshot))]
internal sealed partial class SessionGoldenJsonContext : JsonSerializerContext;
