using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The Step inputs this build can run, built from the <see cref="StepInputRuntime"/> markers AddStepInput registers. A
/// descriptor registered without a runtime (AddBlockDescriptor) validates in a recipe but is not in this set.
/// </summary>
/// <param name="runtimes">The markers.</param>
public sealed class StepInputRuntimes(IEnumerable<StepInputRuntime> runtimes)
{
    private readonly HashSet<BlockId> _ids = [.. runtimes.Select(static r => r.Id)];

    /// <summary>Whether this build runs the input <paramref name="id"/>.</summary>
    /// <param name="id">The input block's id.</param>
    /// <returns>True when it has a runtime.</returns>
    public bool Has(BlockId id) => _ids.Contains(id);
}
