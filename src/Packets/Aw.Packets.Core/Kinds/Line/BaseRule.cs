using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>How a phase picks its base (line spec §8.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BaseRule>))]
public enum BaseRule
{
    /// <summary>The default branch head: no dependency, or every one merged.</summary>
    [JsonStringEnumMemberName("default-branch")]
    DefaultBranch = 1,

    /// <summary>The head branch of the one dependency not merged yet: the PR is stacked on it.</summary>
    [JsonStringEnumMemberName("dependency-head")]
    DependencyHead = 2,
}
