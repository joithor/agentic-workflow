using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IBriefFiles"/>: files by path relative to a root, under the same path rules as the disk reader (no climbing out, no denied glob, at most 1 MiB).</summary>
public sealed class InMemoryBriefFiles : IBriefFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Holds a file.</summary>
    /// <param name="path">The path relative to a root, with '/' separators.</param>
    /// <param name="text">The text.</param>
    /// <returns>The same instance.</returns>
    public InMemoryBriefFiles With(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<BriefFile>> Read(string path, IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(roots);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Find(path, roots));
    }

    private Result<BriefFile> Find(string path, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return SindriErrors.InvalidInput.With("read-brief needs a directory the brief's path is inside");
        }

        var segments = path.Split('/', '\\');
        if (Path.IsPathRooted(path) || segments.Contains("..", StringComparer.Ordinal) || BriefPaths.Denied(path))
        {
            return ScopingErrors.BriefPathRefused.With($"the brief path {path} is refused");
        }

        if (!_files.TryGetValue(path, out var text))
        {
            return SindriErrors.InvalidInput.With($"the brief {path} does not exist");
        }

        return Encoding.UTF8.GetByteCount(text) > BriefPaths.MaxBytes
            ? SindriErrors.InvalidInput.With($"the brief {path} is over {BriefPaths.MaxBytes} bytes")
            : new BriefFile(path, text, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]);
    }
}
