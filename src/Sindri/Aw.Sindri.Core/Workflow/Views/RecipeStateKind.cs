using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Who does a recipe state, as the app shows it: the plain-words kind of a state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecipeStateKind>))]
public enum RecipeStateKind
{
    /// <summary>An agent session does it.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent = 1,

    /// <summary>The engine does it with a block (a deterministic check or a Judge call).</summary>
    [JsonStringEnumMemberName("automatic")]
    Automatic = 2,

    /// <summary>A person does it at the desk.</summary>
    [JsonStringEnumMemberName("you")]
    You = 3,

    /// <summary>A person does it at the desk and nobody else may report it.</summary>
    [JsonStringEnumMemberName("onlyYou")]
    OnlyYou = 4,

    /// <summary>It waits on a child workflow.</summary>
    [JsonStringEnumMemberName("subWorkflow")]
    SubWorkflow = 5,
}
