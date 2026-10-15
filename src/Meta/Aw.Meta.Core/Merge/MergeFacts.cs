using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Merge;

/// <summary>Everything a merge decision looks at.</summary>
public sealed record MergeFacts
{
    /// <summary>The pull request.</summary>
    public required int Pr { get; init; }

    /// <summary>The latest gate run for it, if any.</summary>
    public GateRecord? Gate { get; init; }

    /// <summary>The pull request's head now.</summary>
    public required string CurrentHead { get; init; }

    /// <summary>The main commit now.</summary>
    public required string CurrentMain { get; init; }

    /// <summary>Whether the pull request is a draft.</summary>
    public bool IsDraft { get; init; }

    /// <summary>Paths changed between the gate's head and the current head; null when unknown.</summary>
    public IReadOnlyList<string>? DeltaPaths { get; init; }

    /// <summary>Whether a later gate with the repo scope passed on the current head.</summary>
    public bool RepoGatePassedOnHead { get; init; }

    /// <summary>Pull requests that are open now.</summary>
    public IReadOnlyList<int> OpenPrs { get; init; } = [];

    /// <summary>Merge-order rules.</summary>
    public IReadOnlyList<MergeOrderRule> MergeOrder { get; init; } = [];

    /// <summary>Globs of documentation-only paths.</summary>
    public IReadOnlyList<string> DocsOnly { get; init; } = [];
}
