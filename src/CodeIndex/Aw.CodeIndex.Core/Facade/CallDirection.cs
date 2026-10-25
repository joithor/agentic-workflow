using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>Which way a call walk goes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallDirection>))]
public enum CallDirection
{
    /// <summary>Edges into the symbol.</summary>
    [JsonStringEnumMemberName("callers")]
    Callers,

    /// <summary>Edges out of the symbol.</summary>
    [JsonStringEnumMemberName("callees")]
    Callees,
}
