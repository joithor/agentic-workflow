using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>How the band's states were ordered (screens spec section 5.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PathOrdering>))]
public enum PathOrdering
{
    /// <summary>One initial path.</summary>
    [JsonStringEnumMemberName("path")]
    Path = 1,

    /// <summary>More than one initial path: the transition list order.</summary>
    [JsonStringEnumMemberName("transition-list")]
    TransitionList = 2,
}
