using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Docs;

/// <summary>What the docs check reads out of one markdown file: whether its code fences are balanced, and the relative file links outside them.</summary>
internal static partial class MarkdownCheck
{
    /// <summary>Whether every code fence the file opens is closed.</summary>
    /// <param name="markdown">The file's text.</param>
    /// <returns>True when the fences are balanced.</returns>
    internal static bool FencesBalanced(string markdown) => Lines(markdown).Count(static l => l.Fence) % 2 == 0;

    /// <summary>The link targets that name a file in the repo: not a URL, not a mail link, not a bare anchor, not inside a code fence; the fragment and query are cut off.</summary>
    /// <param name="markdown">The file's text.</param>
    /// <returns>The targets, as written.</returns>
    internal static IReadOnlyList<string> FileLinks(string markdown)
    {
        var open = false;
        List<string> links = [];
        foreach (var (line, fence) in Lines(markdown))
        {
            open ^= fence;
            if (fence || open)
            {
                continue;
            }

            links.AddRange(Link.Matches(line).Select(static m => Target(m.Groups["target"].Value)).Where(static t => t.Length > 0));
        }

        return links;
    }

    private static IEnumerable<(string Line, bool Fence)> Lines(string markdown) =>
        markdown.Split('\n').Select(static l => (l, l.TrimStart().StartsWith("```", StringComparison.Ordinal) || l.TrimStart().StartsWith("~~~", StringComparison.Ordinal)));

    // The path part of a file link; empty for anything that is not one.
    private static string Target(string raw)
    {
        if (raw.Contains("://", StringComparison.Ordinal) || raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || raw.StartsWith('#'))
        {
            return string.Empty;
        }

        var end = raw.IndexOfAny(['#', '?']);
        return end < 0 ? raw : raw[..end];
    }

    [GeneratedRegex(@"\]\(\s*<?(?<target>[^)\s>]+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }
}
