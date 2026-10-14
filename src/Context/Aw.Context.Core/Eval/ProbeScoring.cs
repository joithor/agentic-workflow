using System.Text.RegularExpressions;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Eval;

/// <summary>Scores one answer against a probe (spec §9.2 item 3): precision@5 and citation correctness. Pure.</summary>
public static partial class ProbeScoring
{
    /// <summary>How many of the answer's keys are scored.</summary>
    public const int K = 5;

    /// <summary>The keys an answer names, in order, as scoring reads them: each item's node ('code:P' as 'file:P'), the nodes on an affects item's path, then its citations' keys ('repo:&lt;repo&gt;/P' as 'file:P'). Duplicates are dropped.</summary>
    /// <param name="result">The answer.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlyList<string> Keys(ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var keys = new List<string>();
        foreach (var item in result.Sections.SelectMany(s => s.Items))
        {
            var named = new List<string>();
            if (item.Node is { } node)
            {
                named.Add(node);
            }

            named.AddRange((item.Path ?? []).Skip(1).Select(step => step[(step.IndexOf(' ', StringComparison.Ordinal) + 1)..]));
            named.AddRange(item.Cites.Where(result.Cites.ContainsKey).Select(m => result.Cites[m].Key.Value));
            keys.AddRange(named.Select(Normalized).Where(k => !keys.Contains(k, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal));
        }

        return keys;
    }

    /// <summary>Scores <paramref name="result"/> against <paramref name="probe"/>.</summary>
    /// <param name="probe">The probe.</param>
    /// <param name="result">Its answer.</param>
    /// <returns>The score.</returns>
    public static ProbeScore Score(ProbeCase probe, ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(result);
        var expect = probe.Expect;
        var expected = (expect.Nodes ?? []).Concat(expect.Records ?? []).Concat((expect.Files ?? []).Select(f => "file:" + f)).ToHashSet(StringComparer.Ordinal);
        var keys = Keys(result);
        var top = keys.Take(K).ToList();
        var hits = top.Where(expected.Contains).ToList();
        var absent = (expect.AbsentNodes ?? []).Where(a => keys.Contains(a, StringComparer.Ordinal)).ToList();
        var items = result.Sections.SelectMany(s => s.Items).ToList();
        var cited = items.Count(i => i.Cites.Count > 0 && i.Cites.All(m => result.Cites.TryGetValue(m, out var c) && c.Refs.Count > 0));
        return new ProbeScore(probe.Id, Round(top.Count == 0 ? 0 : (double)hits.Count / top.Count), Round(items.Count == 0 ? 1 : (double)cited / items.Count),
            top, hits, absent);
    }

    internal static double Round(double value) => Math.Round(value, 4, MidpointRounding.ToEven);

    /// <summary>'code:P' and 'repo:&lt;repo&gt;/P' as 'file:P'; other keys as they are.</summary>
    private static string Normalized(string key)
    {
        if (key.StartsWith("code:", StringComparison.Ordinal))
        {
            var path = key["code:".Length..];
            var hash = path.IndexOf('#', StringComparison.Ordinal);
            return "file:" + (hash < 0 ? path : path[..hash]);
        }

        var repo = RepoKey.Match(key);
        return repo.Success ? "file:" + repo.Groups["path"].Value : key;
    }

    [GeneratedRegex("^repo:[^/]+/(?<path>.+)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoKey { get; }
}
