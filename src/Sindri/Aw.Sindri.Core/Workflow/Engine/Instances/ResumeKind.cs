using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>What the answer that ends a suspension does (spec §5.8). Ending it counts no round; a re-run it starts counts one.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResumeKind>))]
public enum ResumeKind
{
    /// <summary>Performs the withheld loop transition into a new entry and run (rounds or no-progress, kind loop).</summary>
    [JsonStringEnumMemberName("transition")]
    Transition = 1,

    /// <summary>Re-runs the Step (a new run in the same entry), with 'fact:answer' when one was given.</summary>
    [JsonStringEnumMemberName("rerun")]
    Rerun = 2,

    /// <summary>Re-evaluates the withheld transition with its stored trigger: every gate and evidence again, at the current head, under the answer's key.</summary>
    [JsonStringEnumMemberName("reevaluate")]
    Reevaluate = 3,

    /// <summary>Waits for the resource again (a resource grant is never stored).</summary>
    [JsonStringEnumMemberName("rewait")]
    Rewait = 4,

    /// <summary>Back to the frame (the same run, the same desk task); the interpreter arms the state's timer afresh. For drift, a frame parked by a Step trigger also re-evaluates it.</summary>
    [JsonStringEnumMemberName("rearm")]
    Rearm = 5,

    /// <summary>A new run of the Step (SND-WF-010), or a rewait for a resource.</summary>
    [JsonStringEnumMemberName("restart")]
    Restart = 6,

    /// <summary>Re-emits the refused effect as a new command with the same effect identity (spec-conflict, dispatch).</summary>
    [JsonStringEnumMemberName("reemit")]
    Reemit = 7,

    /// <summary>'--accept': the withheld transition with the escalated gate overridden; the other gates and evidence still run.</summary>
    [JsonStringEnumMemberName("override")]
    Override = 8,

    /// <summary>A suspension mirroring a child: its answers are forwarded to the child (§5.8), and the child's own changes end it.</summary>
    [JsonStringEnumMemberName("forward")]
    Forward = 9,

    /// <summary>A takeover's handback: back to whatever it covered; the interpreter re-arms the paused timer with what remained.</summary>
    [JsonStringEnumMemberName("return")]
    Return = 10,

    /// <summary>Nothing resumes it (spec-conflict, revoked): only cancel ends it.</summary>
    [JsonStringEnumMemberName("none")]
    None = 11,

    /// <summary>'--reject' on a gate escalation: the escalated gate's refusal stands, and the withheld trigger takes the §5.5 refusal path.</summary>
    [JsonStringEnumMemberName("refuse")]
    Refuse = 12,
}
