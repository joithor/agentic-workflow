using System.Text.Json.Serialization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;

namespace Aw.Context.Testing.Map;

/// <summary>The map file types for tests (in production the Sindri host's JSON context holds them).</summary>
[JsonSerializable(typeof(MapDocument))]
[JsonSerializable(typeof(MapStatusView))]
[JsonSerializable(typeof(MapImported))]
[JsonSerializable(typeof(MapExported))]
[JsonSerializable(typeof(MapFilePreview))]
[JsonSerializable(typeof(MapHealth))]
[JsonSerializable(typeof(ReviewFile))]
internal sealed partial class MapJsonContext : JsonSerializerContext;
