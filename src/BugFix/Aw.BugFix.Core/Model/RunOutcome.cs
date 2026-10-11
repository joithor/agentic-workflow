using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>The verdict of one check run.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunOutcome>))]
public enum RunOutcome
{
    /// <summary>passed.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed = 1,

    /// <summary>failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed = 2,

    /// <summary>broken.</summary>
    [JsonStringEnumMemberName("broken")]
    Broken = 3,
}
