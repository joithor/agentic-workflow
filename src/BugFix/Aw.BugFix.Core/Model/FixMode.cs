using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>How an attempt dispatches implementers: A single, B competing, C split by area.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FixMode>))]
public enum FixMode
{
    /// <summary>A.</summary>
    [JsonStringEnumMemberName("A")]
    A = 1,

    /// <summary>B.</summary>
    [JsonStringEnumMemberName("B")]
    B = 2,

    /// <summary>C.</summary>
    [JsonStringEnumMemberName("C")]
    C = 3,
}
