using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>pr.json of a hand-off packet (line spec §3.2 row 7): the PR the phase opens.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PullRequestFile
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The branch the PR targets.</summary>
    public required string Base { get; init; }

    /// <summary>The phase's head branch.</summary>
    public required string HeadBranch { get; init; }

    /// <summary>The head commit the PR opens at (SHIP-SHA-001).</summary>
    public required CommitSha HeadSha { get; init; }

    /// <summary>The head branch of the PR this one is stacked on; null for a PR on the default branch.</summary>
    public required string? StackParent { get; init; }

    /// <summary>The PR title.</summary>
    public required string Title { get; init; }

    /// <summary>The PR labels.</summary>
    public required IReadOnlyList<string> Labels { get; init; }
}
