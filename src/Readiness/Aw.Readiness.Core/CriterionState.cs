using System.Text.Json.Serialization;

namespace Aw.Readiness.Core;

/// <summary>A criterion's outcome (spec §8). Only pass and fail are scored.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CriterionState>))]
public enum CriterionState
{
    /// <summary>The criterion holds.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The criterion doesn't hold; the finding names the fix.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>The criterion doesn't apply here; the evidence says why.</summary>
    [JsonStringEnumMemberName("n/a")]
    NotApplicable = 3,

    /// <summary>The check can't be reproduced or its data couldn't be read.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown = 4,
}
