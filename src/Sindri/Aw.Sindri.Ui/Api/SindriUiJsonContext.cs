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
[JsonSerializable(typeof(ItemLaunchView))]
[JsonSerializable(typeof(TimelineView))]
[JsonSerializable(typeof(CreateItemRequest))]
[JsonSerializable(typeof(ItemCreatedView))]
[JsonSerializable(typeof(CloseItemRequest))]
[JsonSerializable(typeof(Workflow.InstanceList))]
[JsonSerializable(typeof(Workflow.InstanceDetail))]
[JsonSerializable(typeof(Workflow.FactView))]
[JsonSerializable(typeof(Workflow.RecipeView))]
[JsonSerializable(typeof(Workflow.StatusFields))]
[JsonSerializable(typeof(NeedsYouList))]
[JsonSerializable(typeof(DecisionList))]
[JsonSerializable(typeof(RequestList))]
[JsonSerializable(typeof(DecideRequest))]
[JsonSerializable(typeof(FileRequestRequest))]
[JsonSerializable(typeof(RequestFiledView))]
// LANE ANCHOR U3': one [JsonSerializable] per Needs you record. Insert immediately above this marker, never between the markers.
// LANE ANCHOR U8: one [JsonSerializable] per command send body and reply. Insert immediately below this marker, never between the markers.
internal sealed partial class SindriUiJsonContext : JsonSerializerContext;
