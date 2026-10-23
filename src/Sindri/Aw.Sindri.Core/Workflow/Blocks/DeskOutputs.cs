using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a desk Step's 'complete' takes on each outcome it can report, read from the block descriptor's writes, for the desk report's OutputsByOutcome.</summary>
public static class DeskOutputs
{
    /// <summary>Each outcome of the block to the fact keys ('fact:interviewClosed') the caller has to give on it.</summary>
    /// <param name="catalog">The block catalog.</param>
    /// <param name="block">The full Step id.</param>
    /// <returns>The map; empty when the catalog doesn't know the block.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByOutcome(BlockCatalog catalog, string block)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(block);
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!BlockId.TryFrom(block, out var id) || catalog.Find(id) is not { } descriptor)
        {
            return map;
        }

        foreach (var outcome in descriptor.Outcomes)
        {
            map[outcome.Value] = [.. descriptor.Outputs.Where(o => StepOutputs.CallerWritesOn(o, outcome)).Select(static o => o.Fact.Value)];
        }

        return map;
    }
}
