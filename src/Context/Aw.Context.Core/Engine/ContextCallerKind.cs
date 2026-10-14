using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>Who asks (API spec §4.2): sessions are gated by 'context.sessions.enabled' and scoped to their repo (spec §3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContextCallerKind>))]
public enum ContextCallerKind
{
    /// <summary>The owner, at the CLI or in the app.</summary>
    [JsonStringEnumMemberName("human")]
    Human,

    /// <summary>An agent session, a script or a fetch tool, on any transport.</summary>
    [JsonStringEnumMemberName("session")]
    Session,

    /// <summary>An in-process service (the scheduler, the engine).</summary>
    [JsonStringEnumMemberName("service")]
    Service,
}
