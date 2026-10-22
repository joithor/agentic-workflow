using Aw.Kernel;

namespace Aw.Sindri.Core.Forge;

/// <summary>
/// The pull requests the line follows (PR babysitter spec PB10): Sindri's own port over the forge, with only what the line needs and Sindri's types and strings.
/// Everything it returns from the forge (a check's log, a title) is untrusted text. This build declares the read the ready-check Step needs; the forge wiring adds the rest beside it.
/// </summary>
public interface IPullRequests
{
    /// <summary>Reads a pull request: its state, its head, its checks and what asks for attention.</summary>
    /// <param name="link">The pull request's link, as 'fact:pr' holds it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pull request; an error when the forge can't be reached, the link is refused, or the profile has no forge.</returns>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken);
}
