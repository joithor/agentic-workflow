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
[JsonSerializable(typeof(Workflow.InstanceList))]
[JsonSerializable(typeof(Workflow.InstanceDetail))]
[JsonSerializable(typeof(Workflow.FactView))]
[JsonSerializable(typeof(Workflow.RecipeView))]
[JsonSerializable(typeof(Workflow.StatusFields))]
// LANE ANCHOR U3': add one [JsonSerializable] per Needs you record here.
// LANE ANCHOR U8: add one [JsonSerializable] per command send body and reply here.
internal sealed partial class SindriUiJsonContext : JsonSerializerContext;
