using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Health;

/// <summary>The one-word health of the control plane (UI spec E-14); written in lower case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthWord>))]
public enum HealthWord
{
    /// <summary>Nothing needs attention.</summary>
    [JsonStringEnumMemberName("healthy")]
    Healthy,

    /// <summary>Something needs attention: a failed service, a profile to approve, or the process isn't running.</summary>
    [JsonStringEnumMemberName("degraded")]
    Degraded,

    /// <summary>The projector stalled on an event (SND-VIEW-001).</summary>
    [JsonStringEnumMemberName("stalled")]
    Stalled,

    /// <summary>The views are rebuilding.</summary>
    [JsonStringEnumMemberName("rebuilding")]
    Rebuilding,
}
