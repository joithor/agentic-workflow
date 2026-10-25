namespace Aw.CodeIndex.Testing;

/// <summary>What <see cref="RepoSnapshotContract"/> asks an implementation's fixture to hold: the tree of one commit.</summary>
public sealed class RepoLayout
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly List<string> _symlinks = [];
    private readonly List<string> _submodules = [];
    private readonly List<string> _unsafeNames = [];

    /// <summary>The regular files: path to text.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    /// <summary>The symlink paths.</summary>
    public IReadOnlyList<string> Symlinks => _symlinks;

    /// <summary>The submodule paths.</summary>
    public IReadOnlyList<string> Submodules => _submodules;

    /// <summary>The directory that holds the files with unsafe names, or null.</summary>
    public string? UnsafeDirectory { get; private set; }

    /// <summary>The unsafe file names inside <see cref="UnsafeDirectory"/>.</summary>
    public IReadOnlyList<string> UnsafeNames => _unsafeNames;

    /// <summary>Whether the objects of the regular files are absent from the store (a promisor clone).</summary>
    public bool BlobsMissing { get; private set; }

    /// <summary>Adds a regular file.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The content.</param>
    /// <returns>This layout.</returns>
    public RepoLayout File(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <summary>Adds a symlink.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>This layout.</returns>
    public RepoLayout Symlink(string path)
    {
        _symlinks.Add(path);
        return this;
    }

    /// <summary>Adds a submodule entry.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>This layout.</returns>
    public RepoLayout Submodule(string path)
    {
        _submodules.Add(path);
        return this;
    }

    /// <summary>Adds files whose names git's own checks refuse to stage ('..', '.GIT', a backslash) in one directory.</summary>
    /// <param name="directory">The directory, a plain name.</param>
    /// <param name="names">The file names, single path components.</param>
    /// <returns>This layout.</returns>
    public RepoLayout UnsafeFiles(string directory, params string[] names)
    {
        UnsafeDirectory = directory;
        _unsafeNames.AddRange(names);
        return this;
    }

    /// <summary>Leaves the regular files' objects out of the store, as a promisor clone does.</summary>
    /// <returns>This layout.</returns>
    public RepoLayout WithBlobsMissing()
    {
        BlobsMissing = true;
        return this;
    }
}
