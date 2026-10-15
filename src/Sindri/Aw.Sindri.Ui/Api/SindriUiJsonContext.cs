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
internal sealed partial class SindriUiJsonContext : JsonSerializerContext;
