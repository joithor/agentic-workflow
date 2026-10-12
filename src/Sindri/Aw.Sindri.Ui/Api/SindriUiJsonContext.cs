using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's source-generated JSON context: the API records it reads, through the AwJson profile.</summary>
[JsonSerializable(typeof(ControlRecord))]
[JsonSerializable(typeof(PingView))]
[JsonSerializable(typeof(StatusView))]
[JsonSerializable(typeof(EventView))]
[JsonSerializable(typeof(PreferencesFile))]
[JsonSerializable(typeof(CommandList))]
[JsonSerializable(typeof(ServiceList))]
[JsonSerializable(typeof(CommandAcceptedView))]
[JsonSerializable(typeof(ProblemView))]
[JsonSerializable(typeof(QueueView))]
[JsonSerializable(typeof(ItemView))]
[JsonSerializable(typeof(TimelineView))]
[JsonSerializable(typeof(CreateItemRequest))]
[JsonSerializable(typeof(ItemCreatedView))]
[JsonSerializable(typeof(CloseItemRequest))]
internal sealed partial class SindriUiJsonContext : JsonSerializerContext;
