using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>The engine's ledger event types (spec §5.6).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowEventType>))]
public enum WorkflowEventType
{
    /// <summary>A root started, or a child was created by its parent's transition.</summary>
    [JsonStringEnumMemberName("workflow.started")]
    Started = 1,

    /// <summary>Any state or frame change, including the engine-owned states.</summary>
    [JsonStringEnumMemberName("workflow.transition")]
    Transition = 2,

    /// <summary>A refusal: the gate results and what followed (a re-run, a park or an escalation).</summary>
    [JsonStringEnumMemberName("workflow.refused")]
    Refused = 3,

    /// <summary>A sleep or downtime shift.</summary>
    [JsonStringEnumMemberName("workflow.shifted")]
    Shifted = 4,

    /// <summary>A takeoverIdle timer fired and notified the human.</summary>
    [JsonStringEnumMemberName("workflow.notified")]
    Notified = 5,

    /// <summary>An inbox row expired.</summary>
    [JsonStringEnumMemberName("workflow.expired")]
    Expired = 6,

    /// <summary>Dispatch refused an effect of an instance that had already ended (with a needs-me notice).</summary>
    [JsonStringEnumMemberName("workflow.effect-refused")]
    EffectRefused = 7,

    /// <summary>The instance took or gave up a heavy-job slot (LN-WF3): 'heavy.held' or 'heavy.released'.</summary>
    [JsonStringEnumMemberName("workflow.heavy")]
    Heavy = 8,
}
