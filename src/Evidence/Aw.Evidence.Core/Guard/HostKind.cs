using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Guard;

/// <summary>Where a run executes.</summary>
[JsonConverter(typeof(StrictEnumConverter<HostKind>))]
public enum HostKind
{
    /// <summary>This machine's stack on a loopback origin.</summary>
    [JsonStringEnumMemberName("local")]
    Local,

    /// <summary>A per-PR preview host (a scrubbed customer extract).</summary>
    [JsonStringEnumMemberName("preview")]
    Preview,
}
