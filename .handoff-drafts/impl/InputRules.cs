using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>
/// What the snapshot keeps of a commit's tree (spec §5.1, §9): regular files only, with a safe path, outside the deny globs,
/// under the size caps. Pure, so the git adapter and the fake apply the same rules. Globs are repo-relative with '/'
/// separators, matched without regard to case: '**/' is any directories, a trailing '**' is anything, '*' stays inside one
/// segment, '?' is one character.
/// </summary>
public static class InputRules
{
    private static readonly ConcurrentDictionary<string, Regex> _compiled = new(StringComparer.Ordinal);

    /// <summary>The secret globs every repo has; <see cref="SnapshotLimits.DenyPaths"/> only adds to them.</summary>
    public static IReadOnlyList<string> SecretGlobs { get; } =
    [
        ".env*", "**/.env*", "**/*.pem", "**/*.key", "**/*.p12", "**/*.pfx", "**/id_rsa*", "**/*.tfstate", "**/*.tfvars",
        "**/credentials*", "**/.npmrc", "**/.netrc", "**/secrets/**",
    ];

    /// <summary>Whether every component of a path is safe to write under a snapshot root.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>False for an empty, '..' or '.git' (any case) component, a backslash, a NUL, or an absolute path.</returns>
    public static bool IsSafePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Contains('\\', StringComparison.Ordinal) || path.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        // an absolute path starts with '/', which makes its first component empty
        return path.Split('/').All(c => c.Length > 0 && c != ".." && !string.Equals(c, ".git", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a path matches a built-in secret glob or one of the added ones, ignoring case.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="addedGlobs">The profile's and the repo's globs.</param>
    /// <returns>True when the path is denied.</returns>
    public static bool IsDenied(string path, IReadOnlyList<string> addedGlobs)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(addedGlobs);
        return SecretGlobs.Concat(addedGlobs).Any(g => _compiled.GetOrAdd(g, Compile).IsMatch(path));
    }

    /// <summary>Applies the rules to a commit's tree.</summary>
    /// <param name="commit">The commit the tree belongs to.</param>
    /// <param name="entries">Every blob and submodule entry, as git lists them.</param>
    /// <param name="limits">The deny globs and caps.</param>
    /// <returns>The kept files in path order and a count per skip reason; SND-CODEIDX-003 when the kept sizes or the kept file count pass their cap.</returns>
    public static Result<SnapshotInventory> Apply(CommitSha commit, IEnumerable<TreeEntry> entries, SnapshotLimits limits)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(limits);
        var kept = new List<SnapshotFile>();
        var skipped = new Dictionary<SkipReason, int>();
        foreach (var entry in entries)
        {
            if (Skip(entry, limits) is { } reason)
            {
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
            }
            else
            {
                kept.Add(new SnapshotFile(entry.Path, entry.Blob, entry.Size!.Value));
            }
        }

        if (kept.Count > limits.MaxFiles)
        {
            return CodeIndexErrors.InputTooLarge.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{kept.Count} files are kept; the cap is {limits.MaxFiles}"));
        }

        var total = kept.Sum(f => f.Size);
        if (total > limits.MaxTotalBytes)
        {
            return CodeIndexErrors.InputTooLarge.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{total} bytes are kept; the cap is {limits.MaxTotalBytes}"));
        }

        kept.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return new SnapshotInventory(commit, kept, skipped);
    }

    // The first reason that fits, so every skipped entry is counted once: what it is, then its path, then its size.
    private static SkipReason? Skip(TreeEntry entry, SnapshotLimits limits)
    {
        if (entry.Mode == "120000")
        {
            return SkipReason.Symlink;
        }

        if (entry.Mode == "160000")
        {
            return SkipReason.Submodule;
        }

        // 100644 and 100755 are the regular modes; any other the spec doesn't name is not trusted as a file
        if ((entry.Mode != "100644" && entry.Mode != "100755") || !IsSafePath(entry.Path))
        {
            return SkipReason.UnsafePath;
        }

        if (IsDenied(entry.Path, limits.DenyPaths))
        {
            return SkipReason.Denied;
        }

        return entry.Size switch
        {
            null => SkipReason.Missing,
            var size when size > limits.MaxFileBytes => SkipReason.TooLarge,
            _ => null,
        };
    }

    private static Regex Compile(string glob)
    {
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                if (i + 2 < glob.Length && glob[i + 2] == '/')
                {
                    _ = pattern.Append("(?:.*/)?");
                    i += 2;
                }
                else
                {
                    _ = pattern.Append(".*");
                    i++;
                }
            }
            else
            {
                _ = c switch
                {
                    '*' => pattern.Append("[^/]*"),
                    '?' => pattern.Append("[^/]"),
                    _ => pattern.Append(Regex.Escape(c.ToString())),
                };
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
    }
}
