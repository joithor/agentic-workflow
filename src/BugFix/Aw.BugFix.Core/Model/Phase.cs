using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>A bug fix's phase, in order.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Phase>))]
public enum Phase
{
    /// <summary>intake.</summary>
    [JsonStringEnumMemberName("intake")]
    Intake = 1,

    /// <summary>investigate.</summary>
    [JsonStringEnumMemberName("investigate")]
    Investigate = 2,

    /// <summary>reproduce.</summary>
    [JsonStringEnumMemberName("reproduce")]
    Reproduce = 3,

    /// <summary>fix.</summary>
    [JsonStringEnumMemberName("fix")]
    Fix = 4,

    /// <summary>evaluate.</summary>
    [JsonStringEnumMemberName("evaluate")]
    Evaluate = 5,

    /// <summary>report.</summary>
    [JsonStringEnumMemberName("report")]
    Report = 6,
}
