using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>What a gate's answer meant for the transition (spec §5.8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GateOutcome>))]
public enum GateOutcome
{
    /// <summary>It passed.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed = 1,

    /// <summary>It refused (fail, or a lower-rung gate that is uncertain or below its threshold).</summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 2,

    /// <summary>A top-rung gate was uncertain or below its threshold: the instance escalates.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 3,

    /// <summary>'--accept' overrode it.</summary>
    [JsonStringEnumMemberName("overridden")]
    Overridden = 4,

    /// <summary>No runtime for it in this build: fail closed (W5), a refusal.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable = 5,
}
