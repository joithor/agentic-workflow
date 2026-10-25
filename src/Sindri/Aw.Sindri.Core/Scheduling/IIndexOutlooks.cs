using Aw.Kernel;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>The code index's side of the map publish gate; the Host adapter reads the index, the service and its jobs (CI-9).</summary>
public interface IIndexOutlooks
{
    /// <summary>The outlook of each repo of a hold.</summary>
    /// <param name="hold">The held draft.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outlook by repo name.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken cancellationToken);

    /// <summary>The commit each repo's index names, for the live comparison of Health, status and doctor.</summary>
    /// <param name="repos">The repo names.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The commit by repo name; a repo with no readable index is left out.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken cancellationToken);
}
