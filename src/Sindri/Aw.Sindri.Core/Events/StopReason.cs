using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>Why the control plane stopped; written as its lower-case name ('stop', 'signal', 'port-in-use').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StopReason>))]
public enum StopReason
{
    /// <summary>A client asked ('sindri stop').</summary>
    [JsonStringEnumMemberName("stop")]
    Stop,

    /// <summary>SIGTERM or SIGINT.</summary>
    [JsonStringEnumMemberName("signal")]
    Signal,

    /// <summary>The API port was taken, so it stopped right after taking the lease.</summary>
    [JsonStringEnumMemberName("port-in-use")]
    PortInUse,
}
