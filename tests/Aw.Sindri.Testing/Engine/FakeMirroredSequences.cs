using Aw.Kernel;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Engine;

/// <summary>The ledger rows that mirror Temporal events, by activity id: what a test says the ledger holds.</summary>
public sealed class FakeMirroredSequences : IMirroredSequences
{
    /// <summary>The ledger sequence of the row whose temporal event id is the key.</summary>
    public Dictionary<string, long> Rows { get; } = new(StringComparer.Ordinal);

    /// <summary>The (address, run) pairs it was asked about.</summary>
    public List<(InstanceAddress Address, string RunId)> Asked { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, long>>> Of(InstanceAddress address, string run, IReadOnlyCollection<string> activities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        Asked.Add((address, run));
        IReadOnlyDictionary<string, long> found = activities.Where(Rows.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(static id => id, id => Rows[id], StringComparer.Ordinal);
        return ValueTask.FromResult<Result<IReadOnlyDictionary<string, long>>>(Results.Success(found));
    }
}
