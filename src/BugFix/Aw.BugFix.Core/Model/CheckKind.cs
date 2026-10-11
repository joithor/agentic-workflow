using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>What kind of check reproduces the bug.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckKind>))]
public enum CheckKind
{
    /// <summary>test.</summary>
    [JsonStringEnumMemberName("test")]
    Test = 1,

    /// <summary>ui.</summary>
    [JsonStringEnumMemberName("ui")]
    Ui = 2,
}
