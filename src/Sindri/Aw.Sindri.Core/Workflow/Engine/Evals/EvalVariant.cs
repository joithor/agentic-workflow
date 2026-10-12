using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// Which engine trigger an eval line stands for (spec §9.2, owner decision 2026-10-11): an answer's option with kind
/// 'answer', or the takeover, handback, cancel, deadline or recipe command whose trigger name the line's kind gives.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvalVariant>))]
public enum EvalVariant
{
    /// <summary>'--grant n': n more rounds ('amount').</summary>
    [JsonStringEnumMemberName("grant")]
    Grant = 1,

    /// <summary>'--grant-tokens n': n more tokens ('amount').</summary>
    [JsonStringEnumMemberName("grant-tokens")]
    GrantTokens = 2,

    /// <summary>'--accept': the escalated gate overridden.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept = 3,

    /// <summary>'--reject': the escalated gate's refusal stands.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject = 4,

    /// <summary>'--resume': the frame's resume action.</summary>
    [JsonStringEnumMemberName("resume")]
    Resume = 5,

    /// <summary>'--text': the text is the line's 'outputs' 'fact:answer'.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 6,

    /// <summary>'workflow takeover' (kind 'takeover').</summary>
    [JsonStringEnumMemberName("takeover")]
    Takeover = 7,

    /// <summary>'workflow handback' (kind 'handback').</summary>
    [JsonStringEnumMemberName("handback")]
    Handback = 8,

    /// <summary>'workflow cancel' (kind 'cancel').</summary>
    [JsonStringEnumMemberName("cancel")]
    Cancel = 9,

    /// <summary>The current frame's next deadline fires (kind 'deadline').</summary>
    [JsonStringEnumMemberName("deadline")]
    Deadline = 10,

    /// <summary>A recipe command (kind 'command.&lt;verb&gt;').</summary>
    [JsonStringEnumMemberName("command")]
    Command = 11,
}
