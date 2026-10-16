using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>How a catalog entry takes a Step's policy (launcher L-17); the arguments are added only on an unattended launch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PolicyForm>))]
public enum PolicyForm
{
    /// <summary>The entry can't express a policy; a Step that asks for one is SND-LAUNCH-013.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>'--settings &lt;path&gt;'.</summary>
    [JsonStringEnumMemberName("claude-settings-file")]
    ClaudeSettingsFile,

    /// <summary>One '-c key=value' per <see cref="StepPolicy.Overrides"/> entry.</summary>
    [JsonStringEnumMemberName("codex-overrides")]
    CodexOverrides,

    /// <summary>A cursor config file (unverified; treated as unable to express it until it is).</summary>
    [JsonStringEnumMemberName("cursor-config")]
    CursorConfig,
}
