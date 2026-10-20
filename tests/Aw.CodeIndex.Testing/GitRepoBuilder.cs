using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// Builds a temp git repo (spec §11): files, symlinks, a submodule entry, then one commit; a path swapped after the commit
/// and a promisor clone (blobs left on the origin) are one call each on the built repo or here.
/// </summary>
public sealed class GitRepoBuilder : Builder<GitRepoBuilder, TempGitRepo>
{
    private const string SomeCommit = "1111111111111111111111111111111111111111";
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _symlinks = new(StringComparer.Ordinal);
    private readonly List<string> _submodules = [];

    /// <summary>Adds a regular file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The content.</param>
    /// <returns>This builder.</returns>
    public GitRepoBuilder WithFile(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <summary>Adds a symlink (mode 120000).</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="target">What it points at.</param>
    /// <returns>This builder.</returns>
    public GitRepoBuilder WithSymlink(string path, string target)
    {
        _symlinks[path] = target;
        return this;
    }

    /// <summary>Adds a submodule entry (mode 160000) to the index, with no module behind it.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>This builder.</returns>
    public GitRepoBuilder WithSubmodule(string path)
    {
        _submodules.Add(path);
        return this;
    }

    /// <summary>Makes the repo with one commit on 'main'.</summary>
    /// <returns>The repo.</returns>
    public override TempGitRepo Build()
    {
        var repo = new TempGitRepo(Directory.CreateTempSubdirectory("aw-codeindex-git-").FullName);
        try
        {
            Populate(repo);
            return repo;
        }
        catch (Exception)
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Makes the repo as an origin and clones it with 'blob:none' and no checkout: the clone's tree is complete and its blobs are missing, to be fetched on demand (the promisor fixture). The clone's Origin is deleted with it.</summary>
    /// <returns>The clone.</returns>
    public TempGitRepo BuildPromisorClone()
    {
        using var source = Build();
        var origin = Directory.CreateTempSubdirectory("aw-codeindex-origin-").FullName;
        var clone = Directory.CreateTempSubdirectory("aw-codeindex-clone-").FullName;
        try
        {
            _ = GitRunner.Run(source.Path, "clone", "-q", "--bare", source.Path, origin);
            _ = GitRunner.Run(origin, "config", "uploadpack.allowFilter", "true");
            _ = GitRunner.Run(origin, "config", "uploadpack.allowAnySHA1InWant", "true");
            _ = GitRunner.Run(clone, "-c", "protocol.file.allow=always", "clone", "-q", "--no-checkout", "--filter=blob:none", "--no-local", "file://" + origin, clone);
            return new TempGitRepo(clone, origin);
        }
        catch (Exception)
        {
            Directory.Delete(clone, recursive: true);
            Directory.Delete(origin, recursive: true);
            throw;
        }
    }

    private void Populate(TempGitRepo repo)
    {
        _ = repo.Git("init", "-q", "-b", "main");
        foreach (var (path, text) in _files)
        {
            repo.WriteFile(path, text);
        }

        foreach (var (path, target) in _symlinks)
        {
            var full = Path.Combine(repo.Path, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            _ = File.CreateSymbolicLink(full, target);
        }

        _ = repo.Git("add", "-A");
        foreach (var path in _submodules)
        {
            _ = repo.Git("update-index", "--add", "--cacheinfo", $"160000,{SomeCommit},{path}");
        }

        _ = repo.Git("commit", "-q", "--allow-empty", "-m", "first");
    }
}
