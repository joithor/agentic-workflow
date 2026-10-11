using Aw.Kernel;

namespace Aw.Evidence.Core.Stack;

/// <summary>Reads the local database's profile emails, for the provenance check. The pattern is a bound parameter, never interpolated SQL.</summary>
public interface IProvenanceQuery
{
    /// <summary>Profile emails matching a LIKE pattern.</summary>
    /// <param name="likePattern">The seed pattern.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The emails, or AW-EVIDENCE-014.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> EmailsLikeAsync(string likePattern, CancellationToken cancellationToken);

    /// <summary>Every profile email.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The emails, or AW-EVIDENCE-014.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> AllEmailsAsync(CancellationToken cancellationToken);
}
