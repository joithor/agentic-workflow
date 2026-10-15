using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Why the engine, not a person's workflow, holds an instance (API spec section 3.8): the badge that replaces the glyph.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngineOwnedKind>))]
public enum EngineOwnedKind
{
    /// <summary>The instance waits for an answer.</summary>
    [JsonStringEnumMemberName("parked")]
    Parked = 1,

    /// <summary>A gate or timeout escalated it.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated = 2,

    /// <summary>A person took it over.</summary>
    [JsonStringEnumMemberName("taken-over")]
    TakenOver = 3,
}
