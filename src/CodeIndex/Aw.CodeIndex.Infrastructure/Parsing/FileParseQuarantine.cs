using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>
/// <see cref="IParseQuarantine"/> on two files per repo in one directory: '&lt;repo&gt;.inflight' (the blob being parsed) and
/// '&lt;repo&gt;.quarantine' (one blob id per line, replaced atomically and fsynced). A repo name is file-safe by construction.
/// </summary>
internal sealed class FileParseQuarantine : IParseQuarantine
{
    private readonly string _directory;
    private readonly Dictionary<string, List<string>> _lists = new(StringComparer.Ordinal);

    /// <summary>Creates a quarantine under a directory (created when missing).</summary>
    /// <param name="directory">Where the two files per repo live.</param>
    public FileParseQuarantine(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    /// <inheritdoc />
    public int Recover(RepoName repo)
    {
        var list = Load(repo);
        var inflight = InflightPath(repo);
        if (File.Exists(inflight))
        {
            var blob = File.ReadAllText(inflight).Trim();
            if (IsBlobId(blob) && !list.Contains(blob, StringComparer.Ordinal))
            {
                list.Add(blob);
                Save(repo, list);
            }

            File.Delete(inflight);
        }

        return list.Count;
    }

    /// <inheritdoc />
    public bool Contains(RepoName repo, string blob) => Load(repo).Contains(blob, StringComparer.Ordinal);

    /// <inheritdoc />
    public void Begin(RepoName repo, string blob)
    {
        Require(blob);
        WriteDurably(InflightPath(repo), blob + "\n");
    }

    /// <inheritdoc />
    public void End(RepoName repo) => File.Delete(InflightPath(repo));

    /// <inheritdoc />
    public int Keep(RepoName repo, IReadOnlySet<string> blobsInHead)
    {
        var list = Load(repo);
        if (list.RemoveAll(blob => !blobsInHead.Contains(blob)) > 0)
        {
            Save(repo, list);
        }

        return list.Count;
    }

    private static bool IsBlobId(string value) => value.Length is >= 40 and <= 64 && value.All(Uri.IsHexDigit);

    private static void Require(string blob)
    {
        if (!IsBlobId(blob))
        {
            throw new ArgumentException("A blob id is 40 to 64 hex digits.", nameof(blob));
        }
    }

    private string InflightPath(RepoName repo) => Path.Combine(_directory, repo.Value + ".inflight");

    private string ListPath(RepoName repo) => Path.Combine(_directory, repo.Value + ".quarantine");

    private List<string> Load(RepoName repo)
    {
        if (_lists.TryGetValue(repo.Value, out var cached))
        {
            return cached;
        }

        var path = ListPath(repo);
        var list = File.Exists(path)
            ? File.ReadAllLines(path).Select(line => line.Trim()).Where(IsBlobId).Distinct(StringComparer.Ordinal).ToList()
            : [];
        _lists[repo.Value] = list;
        return list;
    }

    private void Save(RepoName repo, List<string> list)
    {
        if (list.Count > CodeIndexRules.MaxQuarantined)
        {
            list.RemoveRange(0, list.Count - CodeIndexRules.MaxQuarantined);
        }

        var path = ListPath(repo);
        var temporary = path + ".tmp";
        WriteDurably(temporary, string.Concat(list.Select(blob => blob + "\n")));
        File.Move(temporary, path, overwrite: true);
    }

    private static void WriteDurably(string path, string content)
    {
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.WriteThrough };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(path, options);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
}
