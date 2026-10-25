using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The colour class of a state chip.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChipTone>))]
public enum ChipTone
{
    /// <summary>A person has to act.</summary>
    [JsonStringEnumMemberName("attn")]
    Attn = 1,

    /// <summary>Something failed. <see cref="StateChip.For"/> gives it to an instance that ended with outcome 'failed' (FAILED).</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>Working or healthy.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok = 3,

    /// <summary>Neutral.</summary>
    [JsonStringEnumMemberName("neutral")]
    Neutral = 4,
}
