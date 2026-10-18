using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Questions;

/// <summary>The hard check behind a resolution-check; its JSON text is 'ui-evidence' or 'test'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckKind>))]
public enum CheckKind
{
    /// <summary>A browser UI check (ui-evidence).</summary>
    [JsonStringEnumMemberName("ui-evidence")]
    UiEvidence = 1,

    /// <summary>A regression test.</summary>
    [JsonStringEnumMemberName("test")]
    Test = 2,

    /// <summary>An HTTP or CLI contract check (api-evidence).</summary>
    [JsonStringEnumMemberName("api-evidence")]
    ApiEvidence = 3,

    /// <summary>A persisted-state check (db-evidence).</summary>
    [JsonStringEnumMemberName("db-evidence")]
    DbEvidence = 4,
}
