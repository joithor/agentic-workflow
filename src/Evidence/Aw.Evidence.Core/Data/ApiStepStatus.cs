using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>How one api-run step ended.</summary>
[JsonConverter(typeof(StrictEnumConverter<ApiStepStatus>))]
public enum ApiStepStatus
{
    /// <summary>Its expectations held.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>An expectation did not hold.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The request failed or the program would not start.</summary>
    [JsonStringEnumMemberName("broken")]
    Broken,

    /// <summary>A step after a broken one.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
