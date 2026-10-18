using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>What one size class allows a task: at most this many files it may write, acceptance items, and this budget.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SizeLimit
{
    /// <summary>The most files a task's write scope covers (the base commit's files its globs match, plus the paths it declares new).</summary>
    public required int Files { get; init; }

    /// <summary>The most acceptance examples a task holds, held-out ones included.</summary>
    public required int Items { get; init; }

    /// <summary>The most a task's budget may ask for; a limit left out is not checked.</summary>
    public required TaskBudget Budget { get; init; }
}
