using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Usage;

/// <summary>Why a model was invoked.</summary>
[JsonConverter(typeof(StrictEnumConverter<InvocationPhase>))]
public enum InvocationPhase
{
    /// <summary>Writing the script.</summary>
    [JsonStringEnumMemberName("planning")]
    Planning,

    /// <summary>Choosing a replacement for a broken selector.</summary>
    [JsonStringEnumMemberName("selector-repair")]
    SelectorRepair,

    /// <summary>Judging the final screenshot.</summary>
    [JsonStringEnumMemberName("visual-critique")]
    VisualCritique,
}
