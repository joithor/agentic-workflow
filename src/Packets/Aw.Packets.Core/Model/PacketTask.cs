using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Model;

/// <summary>One task (addendum §17.4): dependencies, size, write scope, read seeds, acceptance, rules and budget.</summary>
public sealed record PacketTask
{
    /// <summary>The task id.</summary>
    public required TaskId Id { get; init; }

    /// <summary>The workstream it belongs to.</summary>
    public required WorkstreamId Workstream { get; init; }

    /// <summary>What it does.</summary>
    public required string Title { get; init; }

    /// <summary>The tasks it waits for.</summary>
    public required IReadOnlyList<TaskId> Deps { get; init; }

    /// <summary>Its size class.</summary>
    public required SizeClass Size { get; init; }

    /// <summary>Repo-relative globs it may write, or 'new: &lt;path&gt;' for a file it creates.</summary>
    public required IReadOnlyList<string> WriteScope { get; init; }

    /// <summary>Symbols, product nodes and refs its context starts from.</summary>
    public required IReadOnlyList<string> ReadSeeds { get; init; }

    /// <summary>Its acceptance examples, held-out ones included.</summary>
    public required IReadOnlyList<AcceptanceExample> Acceptance { get; init; }

    /// <summary>Rule selectors (addendum §17.6).</summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    /// <summary>Its budget.</summary>
    public required TaskBudget Budget { get; init; }
}
