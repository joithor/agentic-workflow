using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>How the engine ordered the states of a recipe (E-20).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderedBy>))]
public enum OrderedBy
{
    /// <summary>Path order: the recipe has one initial path.</summary>
    [JsonStringEnumMemberName("path")]
    Path,

    /// <summary>Transition-list order: the graph has more than one initial path.</summary>
    [JsonStringEnumMemberName("transition-list")]
    TransitionList,
}
