using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>What a 'fleet.changed' event says (collector spec CO3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FleetChangeKind>))]
public enum FleetChangeKind
{
    /// <summary>A session appeared, seen in two passes in a row.</summary>
    [JsonStringEnumMemberName("appeared")]
    Appeared,

    /// <summary>A session has been asking for 5 seconds.</summary>
    [JsonStringEnumMemberName("asking")]
    Asking,

    /// <summary>The collector stopped seeing the session's process.</summary>
    [JsonStringEnumMemberName("gone")]
    Gone,
}
