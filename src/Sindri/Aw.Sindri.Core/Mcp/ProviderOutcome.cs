using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Mcp;

/// <summary>What 'sindri mcp register' did, or would do, for one provider.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProviderOutcome>))]
public enum ProviderOutcome
{
    /// <summary>The entry was written (or would be): new, or its port or repo changed.</summary>
    [JsonStringEnumMemberName("registered")]
    Registered,

    /// <summary>The entry is already right; nothing was written.</summary>
    [JsonStringEnumMemberName("unchanged")]
    Unchanged,

    /// <summary>The provider isn't installed.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>Written, but the provider won't load a project file until the human trusts the project.</summary>
    [JsonStringEnumMemberName("needsTrust")]
    NeedsTrust,

    /// <summary>Couldn't register; the detail and fix say why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
