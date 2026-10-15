using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The tone class of a served chip.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChipTone>))]
internal enum ChipTone
{
    [JsonStringEnumMemberName("attn")]
    Attn = 1,

    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    [JsonStringEnumMemberName("ok")]
    Ok = 3,

    [JsonStringEnumMemberName("neutral")]
    Neutral = 4,
}
