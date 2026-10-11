using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>What suspended a frame (spec §5.1, W16).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuspensionKind>))]
public enum SuspensionKind
{
    /// <summary>A park: its deadlines are cleared; the answer re-arms them fresh.</summary>
    [JsonStringEnumMemberName("park")]
    Park = 1,

    /// <summary>An escalation: its deadlines are cleared, like a park.</summary>
    [JsonStringEnumMemberName("escalation")]
    Escalation = 2,

    /// <summary>A takeover: its deadlines are paused, keeping what remained.</summary>
    [JsonStringEnumMemberName("takeover")]
    Takeover = 3,
}
