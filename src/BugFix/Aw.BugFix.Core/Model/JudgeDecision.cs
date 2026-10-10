using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>judge resolution-check's decision on a candidate.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JudgeDecision>))]
public enum JudgeDecision
{
    /// <summary>resolved.</summary>
    [JsonStringEnumMemberName("resolved")]
    Resolved = 1,

    /// <summary>partial.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial = 2,

    /// <summary>unresolved.</summary>
    [JsonStringEnumMemberName("unresolved")]
    Unresolved = 3,

    /// <summary>escalated.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 4,
}
