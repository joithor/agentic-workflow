using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The change port 'read-change' reads through (scoping spec §4, C7): a PR number or a branch of a checkout, resolved to base and head shas, then read.</summary>
public interface IChangeSource
{
    /// <summary>Reads the change <paramref name="reference"/> names.</summary>
    /// <param name="checkout">The repo's checkout.</param>
    /// <param name="reference">A PR number or a branch name.</param>
    /// <param name="read">The cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The change; an invalid-input error when the reference names nothing the checkout holds.</returns>
    public ValueTask<Result<ChangeRecord>> Read(string checkout, string reference, ChangeRead read, CancellationToken cancellationToken);
}
