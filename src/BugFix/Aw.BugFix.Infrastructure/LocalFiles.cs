using System.Security.Cryptography;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>The local file system.</summary>
internal sealed class LocalFiles : IFiles
{
    private const int MaxLinks = 40;

    public string FullPath(string directory, string path) => Path.GetFullPath(path, directory);

    public string? RealPath(string path)
    {
        var real = Resolve(Path.GetFullPath(path), MaxLinks);
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

    // realpath(3): every component resolved through symlinks (git reports /private/var where a path says /var on macOS).
    private static string? Resolve(string full, int budget)
    {
        var current = Path.GetPathRoot(full) ?? "/";
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, part);
            var target = new FileInfo(next).LinkTarget;
            if (target is null)
            {
                current = next;
                continue;
            }

            if (budget == 0)
            {
                return null;
            }

            var resolved = Resolve(Path.GetFullPath(target, current), budget - 1);
            if (resolved is null)
            {
                return null;
            }

            current = resolved;
        }

        return current;
    }
}
