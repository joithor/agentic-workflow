using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>Where a setup step stands; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetupState>))]
public enum SetupState
{
    /// <summary>Not started.</summary>
    [JsonStringEnumMemberName("todo")]
    Todo,

    /// <summary>Under way in the background.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Finished.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>It didn't work, or its state couldn't be read.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Waiting for the human.</summary>
    [JsonStringEnumMemberName("needs-you")]
    NeedsYou,

    /// <summary>Only Onyx has it: it can't be set up on this machine (Docker is missing or fails its checks), which counts as finished.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
