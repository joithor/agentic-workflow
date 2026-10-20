using Aw.Kernel;

namespace Aw.Sindri.Core.Prove;

/// <summary>A commit's message and patch as text, for the canary scan (line spec §9.3). Git is the adapter's; the Step sees only the text.</summary>
public interface ICommitText
{
    /// <summary>The message and patch of one commit.</summary>
    /// <param name="checkout">The repo's checkout.</param>
    /// <param name="sha">The commit's sha (hex).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text; an error when the commit can't be read.</returns>
    public ValueTask<Result<string>> Show(string checkout, string sha, CancellationToken cancellationToken);
}
