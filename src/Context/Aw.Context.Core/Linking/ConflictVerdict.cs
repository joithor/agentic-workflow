using System.Text.Json.Serialization;

namespace Aw.Context.Core.Linking;

/// <summary>What a model check said about two decisions that decide the same node (spec §8.9, A-F13).</summary>
[JsonConverter(typeof(StrictEnumConverter<ConflictVerdict>))]
public enum ConflictVerdict
{
    /// <summary>They can't both hold.</summary>
    [JsonStringEnumMemberName("contradicts")]
    Contradicts,

    /// <summary>They say the same or compatible things.</summary>
    [JsonStringEnumMemberName("agrees")]
    Agrees,

    /// <summary>One replaces the other.</summary>
    [JsonStringEnumMemberName("supersedes")]
    Supersedes,

    /// <summary>The model's answer could not be read; the pair is asked again, up to three tries (<see cref="DecisionConflict.Tries"/>). Not a verdict: it never shows in an answer.</summary>
    [JsonStringEnumMemberName("unread")]
    Unread,

    /// <summary>Three answers in a row could not be read (SND-CONTEXT-057): the pair is given up, not asked again.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,
}
