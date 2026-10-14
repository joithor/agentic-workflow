using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>Map files held in memory by path; <see cref="MapFileReaderContract"/> holds it to the real reader's behavior.</summary>
public sealed class FakeMapFileReader : IMapFileReader
{
    private readonly Dictionary<string, (MapDocument Document, string Hash)> _files = new(StringComparer.Ordinal);

    /// <summary>A path the fake treats as inside a git worktree.</summary>
    public const string WorktreeRoot = "/work/repo";

    /// <summary>Places (or replaces) a file; its hash is that of <paramref name="content"/>.</summary>
    /// <param name="path">The path.</param>
    /// <param name="document">What it parses to.</param>
    /// <param name="content">Its text, for the hash.</param>
    public void Put(string path, MapDocument document, string content) =>
        _files[path] = (document, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));

    /// <inheritdoc/>
    public ValueTask<Result<MapFile>> Read(string path, CancellationToken cancellationToken)
    {
        Result<MapFile> result = path.StartsWith(WorktreeRoot + "/", StringComparison.Ordinal)
            ? ContextErrors.PathInWorktree.With($"{path} is inside the git worktree {WorktreeRoot}")
            : _files.TryGetValue(path, out var file)
                ? new MapFile(file.Document, file.Hash)
                : ContextErrors.MapFileInvalid.With($"{path} can't be read: no such file");
        return ValueTask.FromResult(result);
    }
}
