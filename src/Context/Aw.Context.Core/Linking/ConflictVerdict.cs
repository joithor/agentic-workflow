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
}
