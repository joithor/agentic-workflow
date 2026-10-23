using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>Folders git knows, set by the test: a folder maps to its main checkout and git facts; any other is SND-SETUP-001. Kept honest by <see cref="RepoInspectorContract"/>.</summary>
public sealed class FakeRepoInspector : IRepoInspector
{
    private readonly Dictionary<string, InspectedRepo> _folders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RepoWeight> _weights = new(StringComparer.Ordinal);
    private int _inspected;
    private int _weighed;
    private int _asked;

    /// <summary>Gets how many times <see cref="Inspect"/> was called.</summary>
    public int Inspected => Volatile.Read(ref _inspected);

    /// <summary>Gets how many times <see cref="Facts"/> was called.</summary>
    public int Asked => Volatile.Read(ref _asked);

    /// <summary>Gets how many times <see cref="Weight"/> was called.</summary>
    public int Weighed => Volatile.Read(ref _weighed);

    /// <summary>Sets what <see cref="Weight"/> answers for <paramref name="path"/>.</summary>
    /// <param name="path">The folder, as the profile names it.</param>
    /// <param name="files">How many files git tracks.</param>
    /// <param name="bytes">Their size.</param>
    /// <param name="realPath">The real path it resolves to; <paramref name="path"/> when null.</param>
    /// <returns>This inspector.</returns>
    public FakeRepoInspector Weighs(string path, int files, long bytes, string? realPath = null)
    {
        _weights[path] = new RepoWeight(realPath ?? path, files, bytes);
        return this;
    }

    /// <summary>Makes <paramref name="path"/> a folder that resolves to <paramref name="checkout"/>.</summary>
    /// <param name="path">The folder the owner picks.</param>
    /// <param name="checkout">Its main checkout; the folder itself when null.</param>
    /// <param name="git">What git says of it; main at abc1234 when null.</param>
    /// <returns>This inspector.</returns>
    public FakeRepoInspector Has(string path, string? checkout = null, RepoGit? git = null)
    {
        _folders[path] = new InspectedRepo(checkout ?? path, git ?? new RepoGit("main", "abc1234"));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<InspectedRepo>> Inspect(string path, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _inspected);
        return ValueTask.FromResult<Result<InspectedRepo>>(_folders.TryGetValue(path, out var found) ? found : SindriErrors.SetupFolderInvalid.With("that folder is not a git checkout (fix: pick the repo's top folder)"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<RepoGit>> Facts(string checkout, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _asked);
        var known = _folders.Values.FirstOrDefault(f => string.Equals(f.Checkout, checkout, StringComparison.Ordinal));
        return ValueTask.FromResult<Result<RepoGit>>(known is null ? SindriErrors.SetupFolderInvalid.With("that folder is not a git checkout (fix: pick the repo's top folder)") : known.Git);
    }

    /// <inheritdoc/>
    public ValueTask<Result<RepoWeight>> Weight(string path, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _weighed);
        if (_weights.TryGetValue(path, out var weight))
        {
            return ValueTask.FromResult<Result<RepoWeight>>(weight);
        }

        var known = _folders.TryGetValue(path, out var folder) ? folder : _folders.Values.FirstOrDefault(f => string.Equals(f.Checkout, path, StringComparison.Ordinal));
        return ValueTask.FromResult<Result<RepoWeight>>(known is null ? SindriErrors.SetupFolderInvalid.With("that folder is not a git checkout (fix: pick the repo's top folder)") : new RepoWeight(known.Checkout, 0, 0));
    }
}
