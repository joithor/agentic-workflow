using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What a state timeout does.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimeoutKind>))]
internal enum TimeoutKind
{
    [JsonStringEnumMemberName("parks")]
    Parks = 1,

    [JsonStringEnumMemberName("escalates")]
    Escalates = 2,
}
