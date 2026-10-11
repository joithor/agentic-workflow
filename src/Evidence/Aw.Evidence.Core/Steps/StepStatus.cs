using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Steps;

/// <summary>How a step ended.</summary>
[JsonConverter(typeof(StrictEnumConverter<StepStatus>))]
public enum StepStatus
{
    /// <summary>The action ran and the expected state held.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>The action ran but the expected state did not hold: a behaviour failure, never repaired.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The action itself failed (selector broken) and no repair made it pass.</summary>
    [JsonStringEnumMemberName("broken")]
    Broken,
}
