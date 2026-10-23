using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a desk's person reads before answering (q65): the kind of one thing a desk reviews.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeskReviewKind>))]
public enum DeskReviewKind
{
    /// <summary>A design.</summary>
    [JsonStringEnumMemberName("design")]
    Design = 1,

    /// <summary>A plan.</summary>
    [JsonStringEnumMemberName("plan")]
    Plan = 2,

    /// <summary>A scope packet.</summary>
    [JsonStringEnumMemberName("scope")]
    Scope = 3,

    /// <summary>Another packet (a phase or build packet).</summary>
    [JsonStringEnumMemberName("packet")]
    Packet = 4,

    /// <summary>A pull request.</summary>
    [JsonStringEnumMemberName("pr")]
    Pr = 5,

    /// <summary>Questions the person is asked.</summary>
    [JsonStringEnumMemberName("questions")]
    Questions = 6,

    /// <summary>Plain text (findings, notes).</summary>
    [JsonStringEnumMemberName("text")]
    Text = 7,
}
