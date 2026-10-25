using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The context engine's text shape as the scoping Steps use it: the anchors it suggests for a piece of text (scoping spec §5.1, C1).</summary>
public interface IScopingTextShape
{
    /// <summary>Asks the engine for the anchors <paramref name="text"/> suggests; text over the engine's question limit is cut to its start.</summary>
    /// <param name="repo">The instance's repo.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The suggested anchors, or the error that explains why the engine could not answer (no repo, no profile, a refused question).</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken);
}
