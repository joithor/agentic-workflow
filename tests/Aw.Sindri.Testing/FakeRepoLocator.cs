using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>
/// Repo resolution in memory: each known directory's main checkout (a worktree maps to its main checkout; a directory
/// not in the map is outside git). Paths are already real. Kept honest by <see cref="RepoLocatorContract"/>.
/// </summary>
public sealed class FakeRepoLocator : IRepoLocator
{
    private readonly Dictionary<string, string> _checkouts = new(StringComparer.Ordinal);

    /// <summary>Records that <paramref name="directory"/> belongs to the main checkout <paramref name="checkout"/>.</summary>
    /// <param name="directory">A directory (a worktree, or one inside a checkout).</param>
    /// <param name="checkout">Its main checkout.</param>
    /// <returns>This locator.</returns>
    public FakeRepoLocator In(string directory, string checkout)
    {
        _checkouts[directory] = checkout;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<RepoName?> Locate(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_checkouts.TryGetValue(directory, out var checkout) ? RepoScope.Longest(checkout, repos) : null);

    /// <inheritdoc/>
    public ValueTask<RepoPlacement?> Place(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_checkouts.TryGetValue(directory, out var checkout)
            ? new RepoPlacement(checkout, repos.FirstOrDefault(r => r.Path == checkout && RepoName.TryFrom(r.Name, out _)) is { } entry ? RepoName.From(entry.Name) : null)
            : null);
}
