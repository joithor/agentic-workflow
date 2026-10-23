using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a Step starts with (spec §2). The engine (P3, P4) fills it; later phases add the run token and the frame.</summary>
/// <param name="Step">The Step's id.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Injection">The sections its Step inputs filled.</param>
/// <param name="InputResults">The typed results of its Step inputs by the input block id, or 'id#n' (n = the entry's place in the list) once the Step lists that block twice (W6): read-only, empty when the Step has none, and an input that returned no result is absent.</param>
public sealed record StepContext(
    BlockId Step,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    InjectionSections Injection,
    IReadOnlyDictionary<string, JsonElement>? InputResults = null)
{
    /// <summary>The typed results of the Step's inputs, never null.</summary>
    public IReadOnlyDictionary<string, JsonElement> InputResults { get; init; } = InputResults ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>The repo the instance's item belongs to (a top-level instance's fact:item, or for a child the ledger's repo of the item its address starts with; never from a caller), for a Step that reads or writes through the repo's map; null when the ledger knows none. An item whose repo cannot be read never reaches a Step: the runner fails it first.</summary>
    public RepoName? Repo { get; init; }

    /// <summary>The instance's address ('T-1/scoping/1'), the identity a Step's external writes carry so a retry replaces them; the block runner always sets it. Text, not an InstanceAddress: that type's namespace depends on this one, and a Step that needs the typed id parses it (InstanceAddress.TryFrom).</summary>
    public string? Address { get; init; }

    /// <summary>The last round each of the recipe's loops allows now, by loop name (a granted round counts): what the recipe's '{loop.&lt;name&gt;.cap}' reads. Empty where no recipe runs the Step.</summary>
    public IReadOnlyDictionary<string, int> LoopCaps { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Whether the instance already holds a heavy-job slot for this Step (LN-WF3): a Step that runs a build or a test run takes no second one. False for an instance that never waited on the heavy resource.</summary>
    public bool HeavySlotHeld { get; init; }
}
