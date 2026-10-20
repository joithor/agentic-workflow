using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The profile's repos in memory: the first append creates the profile. Kept honest by <see cref="ProfileRepoWriterContract"/>.</summary>
public sealed class FakeProfileRepoWriter : IProfileRepoWriter
{
    private readonly List<RepoEntry> _repos = [];
    private bool _created;

    /// <summary>Gets the repos written, in order.</summary>
    public IReadOnlyList<RepoEntry> Repos => _repos;

    /// <summary>Gets the effects the profile was created with, or null before it was.</summary>
    public IReadOnlyList<string>? CreatedWith { get; private set; }

    /// <summary>Gets or sets an error Append returns instead of writing.</summary>
    public Error? Fails { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(defaultEffects);
        if (Fails is { } error)
        {
            return ValueTask.FromResult<Result<RepoAppended>>(error);
        }

        if (_repos.Exists(r => r.Name == repo.Name && r.Path == repo.Path))
        {
            return ValueTask.FromResult<Result<RepoAppended>>(RepoAppended.AlreadyThere);
        }

        if (_repos.Exists(r => string.Equals(r.Name, repo.Name, StringComparison.OrdinalIgnoreCase) || r.Path == repo.Path))
        {
            return ValueTask.FromResult<Result<RepoAppended>>(SindriErrors.SetupRepoExists.With("another repo has that name or folder (fix: pick another folder, or give a different name)"));
        }

        _repos.Add(repo);
        if (_created)
        {
            return ValueTask.FromResult<Result<RepoAppended>>(RepoAppended.Appended);
        }

        _created = true;
        CreatedWith = [.. defaultEffects];
        return ValueTask.FromResult<Result<RepoAppended>>(RepoAppended.Created);
    }
}
