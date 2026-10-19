using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features;

/// <summary>The context engine's text shape as the scoping Steps use it: the anchors it suggests for a piece of text (scoping spec §5.1, C1).</summary>
internal interface IScopingTextShape
{
    /// <summary>Asks the engine for the anchors <paramref name="text"/> suggests.</summary>
    /// <param name="repo">The instance's repo.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The 'contextQuery' fact's JSON text, or the error that explains why the engine could not answer.</returns>
    public ValueTask<Result<string>> Anchors(RepoName? repo, string text, CancellationToken cancellationToken);
}
