using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>Where a bug fix stands overall.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BugFixStatus>))]
public enum BugFixStatus
{
    /// <summary>active.</summary>
    [JsonStringEnumMemberName("active")]
    Active = 1,

    /// <summary>resolved.</summary>
    [JsonStringEnumMemberName("resolved")]
    Resolved = 2,

    /// <summary>unresolved.</summary>
    [JsonStringEnumMemberName("unresolved")]
    Unresolved = 3,

    /// <summary>needs-human.</summary>
    [JsonStringEnumMemberName("needs-human")]
    NeedsHuman = 4,
}
