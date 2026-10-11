using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>What the investigation found about a hypothesis.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HypothesisResult>))]
public enum HypothesisResult
{
    /// <summary>confirmed.</summary>
    [JsonStringEnumMemberName("confirmed")]
    Confirmed = 1,

    /// <summary>ruled-out.</summary>
    [JsonStringEnumMemberName("ruled-out")]
    RuledOut = 2,

    /// <summary>untested.</summary>
    [JsonStringEnumMemberName("untested")]
    Untested = 3,
}
