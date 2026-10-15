using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The colour class of a state chip.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChipTone>))]
public enum ChipTone
{
    /// <summary>A person has to act.</summary>
    [JsonStringEnumMemberName("attn")]
    Attn = 1,

    /// <summary>Something failed. Reserved: <see cref="StateChip.For"/> produces no failed chip yet (the screens fixture has none); a failed or cancelled terminal state reads DONE until the owner table adds a word.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>Working or healthy.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok = 3,

    /// <summary>Neutral.</summary>
    [JsonStringEnumMemberName("neutral")]
    Neutral = 4,
}
