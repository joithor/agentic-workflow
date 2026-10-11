using System.Security.Cryptography;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>The local file system.</summary>
internal sealed class LocalFiles : IFiles
{
    private const int MaxLinks = 40;

    // Joined, not normalized: ".." must be applied after the links before it are resolved (RealPath), as the OS does.
    public string FullPath(string directory, string path) => Path.IsPathRooted(path) ? path : Path.Join(directory, path);

    public string? RealPath(string path)
    {
        var real = Path.IsPathRooted(path) ? Resolve(path, MaxLinks) : null;
        return real is not null && File.Exists(real) ? real : null;
    }

    public Result<string> ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BugFixErrors.FileUnreadable.With($"cannot read {path}: {ex.Message}");
        }
    }

    public Result<string> Sha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BugFixErrors.FileUnreadable.With($"cannot read {path}: {ex.Message}");
        }
    }

    // realpath(3): components left to right, each link resolved before the next component, and ".." applied to the
    // resolved directory (so "link/.." is the link target's parent, as the OS sees it, not the link's).
    private static string? Resolve(string path, int budget)
    {
        var current = Path.GetPathRoot(path) ?? "/";
        foreach (var part in path[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            var next = Path.Join(current, part);
            if (new FileInfo(next).LinkTarget is not { } target)
            {
                current = next;
                continue;
            }

            if (budget == 0 || Resolve(Path.IsPathRooted(target) ? target : Path.Join(current, target), budget - 1) is not { } resolved)
            {
                return null;
            }

            current = resolved;
        }

        return current;
    }
}
