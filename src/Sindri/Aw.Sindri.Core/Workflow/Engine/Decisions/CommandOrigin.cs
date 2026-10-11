using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>Who an outbox command comes from (spec §5.4): the origin decides the exemption, not the kind.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandOrigin>))]
public enum CommandOrigin
{
    /// <summary>The engine's own notifications (parks, escalations, desk tasks, takeoverIdle): only 'notify.desk', exempt from the approval check and the allowlist.</summary>
    [JsonStringEnumMemberName("engine")]
    Engine = 1,

    /// <summary>A recipe transition's effect: checked like any effect.</summary>
    [JsonStringEnumMemberName("recipe")]
    Recipe = 2,
}
