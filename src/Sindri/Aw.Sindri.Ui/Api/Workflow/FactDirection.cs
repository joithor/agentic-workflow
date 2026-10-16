using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Whether a fact enters or leaves the instance.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactDirection>))]
internal enum FactDirection
{
    [JsonStringEnumMemberName("input")]
    Input = 1,

    [JsonStringEnumMemberName("output")]
    Output = 2,
}
