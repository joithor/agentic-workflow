using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>Which answer a park or escalation takes (E-23).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnswerKind>))]
public enum AnswerKind
{
    /// <summary>An answer text.</summary>
    [JsonStringEnumMemberName("text")]
    Text,

    /// <summary>A resume, optionally with text.</summary>
    [JsonStringEnumMemberName("resume")]
    Resume,

    /// <summary>A grant of more rounds.</summary>
    [JsonStringEnumMemberName("grant")]
    Grant,

    /// <summary>A grant of more tokens.</summary>
    [JsonStringEnumMemberName("grantTokens")]
    Granttokens,

    /// <summary>An accept of the escalation.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept,

    /// <summary>No answer; cancel is the way out.</summary>
    [JsonStringEnumMemberName("cancel-only")]
    CancelOnly,
}
