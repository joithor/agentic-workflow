using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>What followed a refusal (spec §5.8, W14): every refusal consumes its trigger, and one of these follows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RefusalFollow>))]
public enum RefusalFollow
{
    /// <summary>Below the loop's cap: the Step re-runs with the refusal as findings (run n+1, one round).</summary>
    [JsonStringEnumMemberName("rerun")]
    Rerun = 1,

    /// <summary>The instance parked (budget, rounds, no-progress or needs-answer).</summary>
    [JsonStringEnumMemberName("park")]
    Park = 2,

    /// <summary>A top-rung gate escalated.</summary>
    [JsonStringEnumMemberName("escalate")]
    Escalate = 3,

    /// <summary>
    /// A command was refused with SND-WF-004, recorded once under its nonce. Refused outright it changes no row; refused
    /// on an answer's resume ('--reject', or '--accept' with another check refusing), the answer's suspension ends with it.
    /// </summary>
    [JsonStringEnumMemberName("command-refused")]
    CommandRefused = 4,
}
