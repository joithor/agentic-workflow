using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>How an api-run ended; the exit code alone decides it (0 passed, 2 failed, 1 refused, 4 errored).</summary>
[JsonConverter(typeof(StrictEnumConverter<ApiOutcome>))]
public enum ApiOutcome
{
    /// <summary>Every expectation held.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>An expectation did not hold.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>A guard or an invalid input refused the run.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,

    /// <summary>The run itself errored.</summary>
    [JsonStringEnumMemberName("errored")]
    Errored,
}
