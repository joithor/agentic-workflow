using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>What suspended an instance (spec §5.1, §5.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuspensionKind>))]
public enum SuspensionKind
{
    /// <summary>A park: the interpreter stops the state's timer; the answer arms it again with the full timeout.</summary>
    [JsonStringEnumMemberName("park")]
    Park = 1,

    /// <summary>An escalation: like a park, a human decides.</summary>
    [JsonStringEnumMemberName("escalation")]
    Escalation = 2,

    /// <summary>A takeover: the interpreter pauses the state's timer and keeps what remained for the handback.</summary>
    [JsonStringEnumMemberName("takeover")]
    Takeover = 3,
}
