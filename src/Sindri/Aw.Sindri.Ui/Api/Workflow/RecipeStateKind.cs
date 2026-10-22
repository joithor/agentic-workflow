using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Who does a recipe state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeStateKind>))]
internal enum RecipeStateKind
{
    [JsonStringEnumMemberName("agent")]
    Agent = 1,

    [JsonStringEnumMemberName("automatic")]
    Automatic = 2,

    [JsonStringEnumMemberName("you")]
    You = 3,

    [JsonStringEnumMemberName("onlyYou")]
    OnlyYou = 4,

    [JsonStringEnumMemberName("subWorkflow")]
    SubWorkflow = 5,
}
