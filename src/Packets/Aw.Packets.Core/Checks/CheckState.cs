using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Checks;

/// <summary>A check's outcome.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckState>))]
public enum CheckState
{
    /// <summary>The check ran and holds.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The check ran and found a violation.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>The check can't run yet (its inputs or policy aren't defined); the reason says why.</summary>
    [JsonStringEnumMemberName("unchecked")]
    Unchecked = 3,
}
