using System.Text.RegularExpressions;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>
/// The files of the per-repo indexes in one directory (spec §4.1): <c lang="text">&lt;repo&gt;.&lt;ulid&gt;.db</c> index files, the one-line
/// pointer <c lang="text">&lt;repo&gt;.current</c> that names the live ULID, and <c lang="text">&lt;repo&gt;.building</c>, which names the running build's file.
/// </summary>
internal sealed class IndexFiles(string directory)
{
    private static readonly TimeSpan _leftoverAge = TimeSpan.FromMinutes(10);
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = PrivateFile | UnixFileMode.UserExecute;

    internal string Root { get; } = directory;

    internal string DbPath(RepoName repo, string ulid) => Path.Combine(Root, $"{repo.Value}.{ulid}.db");

    internal string PointerPath(RepoName repo) => Path.Combine(Root, $"{repo.Value}.current");

    internal string BuildingPath(RepoName repo) => Path.Combine(Root, $"{repo.Value}.building");

    /// <summary>Reads the pointer without following a symlink and without creating anything.</summary>
    /// <returns>The live ULID, null when there is no pointer, or SND-CODEIDX-010 when the pointer is a link or not a ULID.</returns>
    internal Result<string?> ReadPointer(RepoName repo)
    {
        var pointer = new FileInfo(PointerPath(repo));
        if (pointer.LinkTarget is not null)
        {
            return Damaged("the pointer is a symbolic link");
        }

        if (!pointer.Exists)
        {
            return (string?)null;
        }

        var text = pointer.Length is > 0 and <= 64 ? File.ReadAllText(pointer.FullName).Trim() : string.Empty;
        return Ulid.IsValid(text) ? (Result<string?>)text : Damaged("the pointer does not name a build file");
    }

    /// <summary>The live index file the pointer names.</summary>
    /// <returns>Its path, null when there is no pointer, or SND-CODEIDX-010 when the pointer is bad or the file is missing or a link.</returns>
    internal Result<string?> LivePath(RepoName repo)
    {
        var pointer = ReadPointer(repo);
        if (pointer.IsError || pointer.Value is not { } ulid)
        {
            return pointer;
        }

        var file = new FileInfo(DbPath(repo, ulid));
        return file.LinkTarget is null && file.Exists ? (Result<string?>)file.FullName : Damaged("the pointer names a file that is not there");
    }

    internal void EnsureDirectory() => _ = OperatingSystem.IsWindows() ? Directory.CreateDirectory(Root) : Directory.CreateDirectory(Root, PrivateDirectory);

    /// <summary>Creates an empty file, private to the owner, that must not exist yet.</summary>
    internal static FileStream CreatePrivate(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFile;
        }

        return new FileStream(path, options);
    }

    /// <summary>Publishes a new live ULID: writes <c lang="text">.current.tmp</c> beside the pointer, then renames it over the pointer.</summary>
    internal async Task SwapPointer(RepoName repo, string ulid, CancellationToken cancellationToken)
    {
        var temporary = PointerPath(repo) + ".tmp";
        File.Delete(temporary);
        var stream = CreatePrivate(temporary);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(ulid + "\n"), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, PointerPath(repo), overwrite: true);
    }

    internal static void DeleteWithSidecars(string path)
    {
        DeleteSidecars(path);
        File.Delete(path);
    }

    internal static void DeleteSidecars(string path)
    {
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
    }

    /// <summary>Deletes index files of the repo (exactly the index file pattern) that no pointer names, that are not the running build's file and that are older than 10 minutes.</summary>
    internal void Sweep(RepoName repo, DateTimeOffset now)
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        var pattern = IndexFilePattern(repo);
        var keep = new HashSet<string>(StringComparer.Ordinal);
        if (ReadPointer(repo) is { IsSuccess: true } pointer && pointer.Value is { } live)
        {
            _ = keep.Add(live);
        }

        if (RecordedBuild(repo) is { } building)
        {
            _ = keep.Add(building);
        }

        foreach (var file in new DirectoryInfo(Root).EnumerateFiles($"{repo.Value}.*"))
        {
            var match = pattern.Match(file.Name);
            if (match.Success && !keep.Contains(match.Groups["ulid"].Value) && now - new DateTimeOffset(file.LastWriteTimeUtc) > _leftoverAge)
            {
                file.Delete();
            }
        }
    }

    internal void RecordBuild(RepoName repo, string ulid) => File.WriteAllText(BuildingPath(repo), ulid + "\n");

    internal void ClearBuild(RepoName repo, string ulid)
    {
        if (string.Equals(RecordedBuild(repo), ulid, StringComparison.Ordinal))
        {
            File.Delete(BuildingPath(repo));
        }
    }

    private string? RecordedBuild(RepoName repo)
    {
        var path = BuildingPath(repo);
        return File.Exists(path) && File.ReadAllText(path).Trim() is { } text && Ulid.IsValid(text) ? text : null;
    }

    private static Regex IndexFilePattern(RepoName repo)
        => new($"^{Regex.Escape(repo.Value)}\\.(?<ulid>[0-9A-HJKMNP-TV-Z]{{26}})\\.db(-wal|-shm)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static Error Damaged(string why) => CodeIndexErrors.IndexDamaged.With($"The index pointer is unusable: {why}.");
}
