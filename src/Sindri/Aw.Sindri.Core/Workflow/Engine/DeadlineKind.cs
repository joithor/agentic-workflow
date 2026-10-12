using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>Which of the interpreter's timers fired (spec §5.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeadlineKind>))]
public enum DeadlineKind
{
    /// <summary>The state's timeout ('timeout.after'), armed on entry and again after an answer.</summary>
    [JsonStringEnumMemberName("state")]
    State = 1,

    /// <summary>The timer a 'then: retry' re-arms once in the same entry; when it fires the instance parks with 'timeout'.</summary>
    [JsonStringEnumMemberName("retry")]
    Retry = 2,

    /// <summary>The takeover's idle reminder (2 h): it notifies the human once and clears, and never hands back (W19).</summary>
    [JsonStringEnumMemberName("takeoverIdle")]
    TakeoverIdle = 3,
}
