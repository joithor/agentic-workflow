using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's source-generated JSON context: the API records it reads, through the AwJson profile.</summary>
[JsonSerializable(typeof(ControlRecord))]
[JsonSerializable(typeof(PingView))]
[JsonSerializable(typeof(StatusView))]
[JsonSerializable(typeof(EventView))]
internal sealed partial class SindriUiJsonContext : JsonSerializerContext;
