using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The Steps this build has a block runtime for (DT spec §8.2), from the AddBlockStep registrations. A singleton.</summary>
public sealed class BlockStepRuntimes
{
    /// <summary>Collects the registrations.</summary>
    /// <param name="registrations">Every AddBlockStep registration.</param>
    public BlockStepRuntimes(IEnumerable<BlockStepRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var all = registrations.ToList();
        Ids = all.Select(static r => r.Step).ToHashSet();
        _repoSteps = [.. all.Where(static r => r.NeedsRepo).Select(static r => r.Step)];
    }

    private readonly HashSet<BlockId> _repoSteps;

    /// <summary>Whether the Step reads the instance's repo (it is an <see cref="IRepoStep"/>).</summary>
    /// <param name="step">The Step.</param>
    /// <returns>True when it does.</returns>
    public bool NeedsRepo(BlockId step) => _repoSteps.Contains(step);

    /// <summary>The registered Steps.</summary>
    public IReadOnlySet<BlockId> Ids { get; }
}
