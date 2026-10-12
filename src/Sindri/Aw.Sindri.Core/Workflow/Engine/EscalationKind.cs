using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>Why an instance escalated (spec §3.8).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EscalationKind>))]
public enum EscalationKind
{
    /// <summary>A top-rung gate scored below its threshold or was uncertain; '--accept' overrides only that gate, '--reject' is a refusal.</summary>
    [JsonStringEnumMemberName("gate")]
    Gate = 1,

    /// <summary>A state ran past its timeout with 'then: escalate'; '--resume' gives it the full time again.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout = 2,

    /// <summary>The child escalated; its answers are forwarded to it (§5.8).</summary>
    [JsonStringEnumMemberName("child")]
    Child = 3,
}
