using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How the states were ordered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PathOrdering>))]
internal enum PathOrdering
{
    [JsonStringEnumMemberName("path")]
    Path = 1,

    [JsonStringEnumMemberName("transition-list")]
    TransitionList = 2,
}
