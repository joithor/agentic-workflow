using System.Text.Json.Serialization;

namespace Aw.Context.Core.Code;

/// <summary>How the own scope touches a data thing (S-CL).</summary>
[JsonConverter(typeof(StrictEnumConverter<DataAccess>))]
public enum DataAccess
{
    /// <summary>Only 'reads' edges.</summary>
    [JsonStringEnumMemberName("reads")]
    Reads,

    /// <summary>Only 'writes' edges.</summary>
    [JsonStringEnumMemberName("writes")]
    Writes,

    /// <summary>Both.</summary>
    [JsonStringEnumMemberName("both")]
    Both,
}
