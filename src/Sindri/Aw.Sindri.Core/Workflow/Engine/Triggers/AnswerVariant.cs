using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>Which 'workflow answer' option was given (spec §6.3, §3.8). Grants and accept are protected (W7, W22).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerVariant>))]
public enum AnswerVariant
{
    /// <summary>'--text' or '--file': recorded as 'fact:answer'; satisfies an answer wait with outcome 'answered'.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>'--grant n' (protected): n more rounds on a rounds or no-progress park.</summary>
    [JsonStringEnumMemberName("grant")]
    Grant = 2,

    /// <summary>'--grant-tokens n' (protected): more tokens on a budget park, at most the original budget, once per park.</summary>
    [JsonStringEnumMemberName("grant-tokens")]
    GrantTokens = 3,

    /// <summary>'--accept' (protected): overrides the escalated gate only.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept = 4,

    /// <summary>'--reject': the escalated gate's refusal stands (the §5.8 refusal path).</summary>
    [JsonStringEnumMemberName("reject")]
    Reject = 5,

    /// <summary>'--resume [--text]': runs the frame's resume action.</summary>
    [JsonStringEnumMemberName("resume")]
    Resume = 6,
}
