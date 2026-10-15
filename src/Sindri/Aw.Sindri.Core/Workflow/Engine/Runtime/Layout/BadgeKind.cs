using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>What interrupts an instance (E-20): the badge that replaces the state's glyph.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BadgeKind>))]
public enum BadgeKind
{
    /// <summary>The engine parked the instance.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked,

    /// <summary>A gate escalated the instance.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated,

    /// <summary>A human took the instance over.</summary>
    [JsonStringEnumMemberName("taken-over")]
    TakenOver,
}
