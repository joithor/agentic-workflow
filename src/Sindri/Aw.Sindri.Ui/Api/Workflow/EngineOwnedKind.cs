using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Why the engine owns a state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngineOwnedKind>))]
internal enum EngineOwnedKind
{
    [JsonStringEnumMemberName("parked")]
    Parked = 1,

    [JsonStringEnumMemberName("escalated")]
    Escalated = 2,

    [JsonStringEnumMemberName("taken-over")]
    TakenOver = 3,
}
