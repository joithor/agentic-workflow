using System.Text;
using System.Text.RegularExpressions;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// One write-scope entry: a repo-relative glob ('*' and '?' stay inside a path segment, '**' crosses segments), or
/// 'new: &lt;path&gt;' for a file the task creates.
/// </summary>
internal sealed class ScopeGlob
{
    private const string NewPrefix = "new:";

    /// <summary>The longest entry accepted; a longer one is a PKT-SCOPE-001 failure.</summary>
    internal const int MaxLength = 1024;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);
    private readonly Regex? _pattern;

    private ScopeGlob(string entry, string? newPath, Regex? pattern)
    {
        Entry = entry;
        NewPath = newPath;
        _pattern = pattern;
    }

    /// <summary>The entry as written.</summary>
    public string Entry { get; }

    /// <summary>The path a 'new:' entry declares; null for a glob.</summary>
    public string? NewPath { get; }

    /// <summary>Parses one entry; null when it is empty or a 'new:' path isn't a safe relative path.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The scope, or null.</returns>
    public static ScopeGlob? Parse(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Length > MaxLength)
        {
            return null;
        }

        if (entry.StartsWith(NewPrefix, StringComparison.Ordinal))
        {
            var path = entry[NewPrefix.Length..].Trim();
            return RelativePath.IsSafe(path) ? new ScopeGlob(entry, path, pattern: null) : null;
        }

        if (entry.Trim().Length == 0)
        {
            return null;
        }

        try
        {
            return new ScopeGlob(entry, newPath: null, ToRegex(Collapse(entry.Trim())));
        }
        catch (NotSupportedException)
        {
            return null;   // the NonBacktracking engine refuses a pattern whose automaton is too large
        }
    }

    /// <summary>True when the glob matches <paramref name="path"/>, or a 'new:' entry declares exactly it.</summary>
    /// <param name="path">A repo-relative path with '/' separators.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string path) =>
        _pattern?.IsMatch(path) ?? string.Equals(NewPath, path, StringComparison.Ordinal);

    // '**/**/' matches what '**/' matches: collapse the runs, so the pattern stays small.
    private static string Collapse(string glob)
    {
        var collapsed = glob;
        while (collapsed.Contains("**/**/", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("**/**/", "**/", StringComparison.Ordinal);
        }

        return collapsed;
    }

    // NonBacktracking: matching is linear in the path's length whatever the glob (these patterns have no lookarounds).
    private static Regex ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            if (string.CompareOrdinal(glob, i, "**/", 0, 3) == 0)
            {
                _ = pattern.Append("(?:[^/]*/)*");
                i += 2;
            }
            else if (string.CompareOrdinal(glob, i, "**", 0, 2) == 0)
            {
                _ = pattern.Append(".*");
                i++;
            }
            else
            {
                _ = pattern.Append(glob[i] switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(glob[i].ToString()),
                });
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, _timeout);
    }
}
