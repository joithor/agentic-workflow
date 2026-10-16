using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How far a fact is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FactStatus>))]
internal enum FactStatus
{
    [JsonStringEnumMemberName("produced")]
    Produced = 1,

    [JsonStringEnumMemberName("in-progress")]
    InProgress = 2,

    [JsonStringEnumMemberName("pending")]
    Pending = 3,
}
