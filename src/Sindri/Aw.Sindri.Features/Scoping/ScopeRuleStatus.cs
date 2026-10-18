using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>A rule's live status in one run.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScopeRuleStatus>))]
public enum ScopeRuleStatus
{
    /// <summary>The rule ran and found nothing.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The rule ran and has a failing finding.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>The rule ran and has only warnings.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn = 3,

    /// <summary>The rule needs facts the directory lacks (never a pass at evidence), or belongs to the other stage.</summary>
    [JsonStringEnumMemberName("not-run")]
    NotRun = 4,
}
