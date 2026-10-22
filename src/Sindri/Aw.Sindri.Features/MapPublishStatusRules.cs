using System.Globalization;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features;

/// <summary>The publish state and its words (code index spec 6.4, "What the owner sees"): pure, computed live from stored state, never stored.</summary>
internal static class MapPublishStatusRules
{
    internal const string Held = "held";
    internal const string Overdue = "overdue";
    internal const string Published = "published";
    internal const string PublishedWithoutWaiting = "published-without-waiting";
    internal const string PublishedStale = "published-stale";
    internal const string PublishedByHand = "published-by-hand";

    private const int LinesShown = 2;

    // The tick is a minute: a held map is overdue once its deadline is more than two ticks behind.
    private static readonly TimeSpan _overdueAfter = TimeSpan.FromMinutes(2);

    /// <summary>The status for <paramref name="facts"/> at <paramref name="now"/>.</summary>
    /// <param name="facts">The stored state and the index's commits.</param>
    /// <param name="now">The time.</param>
    /// <returns>The state, its words and the stale repos.</returns>
    internal static MapPublishStatusView Of(MapPublishFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.HeldVersion is { } held)
        {
            return HeldView(facts, held, now);
        }

        var stale = StaleOf(facts.Repos);
        var version = facts.Current ?? 0;
        if (stale.Count > 0)
        {
            return new MapPublishStatusView(PublishedStale, "published, code answers stale", facts.Reason, Stale: true, stale, version, Until: null, StaleLines(version, facts, stale));
        }

        if (facts.ByHand)
        {
            return Plain(PublishedByHand, "published by hand", facts, Text($"Map v{version} was published by hand."));
        }

        return WhyNotWaited(facts.Reason) is { } why
            ? Plain(PublishedWithoutWaiting, "published without waiting", facts, Text($"Map v{version} was published without waiting for the code index ({why})."))
            : Plain(Published, "published", facts, PublishedLine(version, facts.Repos));
    }

    /// <summary>The short form of a commit: its first seven characters.</summary>
    /// <param name="commit">The commit.</param>
    /// <returns>Seven characters at most.</returns>
    internal static string Short(string commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return commit.Length > 7 ? commit[..7] : commit;
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static MapPublishStatusView HeldView(MapPublishFacts facts, int held, DateTimeOffset now)
    {
        var until = (facts.Since < now ? facts.Since : now) + facts.Wait;
        var overdue = now > until + _overdueAfter;
        var waiting = facts.Repos.Where(static r => r.Waiting).ToList();
        var at = until.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + "Z";
        List<string> lines = [];
        if (overdue)
        {
            lines.Add(Text($"Map v{held} is overdue: its wait ended at {at} and it is still held (the map service is paused, or the publish is failing)."));
        }
        else if (waiting.Count == 0)
        {
            lines.Add(Text($"Map v{held} is held; none of your repos are waiting."));
        }
        else
        {
            lines.Add(Text($"Map v{held} is held for the code index until {at}."));
        }

        lines.AddRange(Capped(waiting.Select(static r => $"{r.Repo}: {WaitWord(r.Word)} (the map is for {Short(r.MapCommit ?? "?")}, the index is at {(r.IndexCommit is null ? "nothing yet" : Short(r.IndexCommit))})")));
        return new MapPublishStatusView(overdue ? Overdue : Held, overdue ? "overdue" : "held for the code index", Reason: null, Stale: false, [], held, until, lines);
    }

    private static MapPublishStatusView Plain(string state, string word, MapPublishFacts facts, string line) =>
        new(state, word, facts.Reason, Stale: false, [], facts.Current ?? 0, Until: null, [line]);

    private static List<StaleRepo> StaleOf(IReadOnlyList<PublishRepoFact> repos) =>
        [.. repos.Where(static r => r.MapCommit is not null && r.IndexCommit is not null && !Same(r.MapCommit, r.IndexCommit))
            .Select(static r => new StaleRepo(r.Repo, Short(r.IndexCommit ?? string.Empty), Short(r.MapCommit ?? string.Empty)))];

    // Heads are stored as 7 to 40 hex characters and the index may name the full commit: the shorter is a prefix of the longer.
    private static bool Same(string a, string b) => a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal);

    private static List<string> StaleLines(int version, MapPublishFacts facts, List<StaleRepo> stale)
    {
        var several = facts.Repos.Count > 1;
        var relation = facts.Reason switch
        {
            "IndexReady" => "newer than",
            "TimedOut" => "older than",
            "FirstMap" => "older than",
            "WaitOff" => "older than",
            "IndexBroken" => "older than",
            _ => "not the commit of",
        };
        return Capped(stale.Select(s => Text($"{(several ? s.Repo + ": " : string.Empty)}Code answers are from {s.IndexCommit} ({relation} map v{version}, which is for {s.MapCommit}).")));
    }

    private static string PublishedLine(int version, IReadOnlyList<PublishRepoFact> repos)
    {
        var matching = repos.Where(static r => r.IndexCommit is not null).Select(static r => r.Repo).ToList();
        var missing = repos.Where(static r => r.IndexCommit is null).Select(static r => r.Repo).ToList();
        var line = Text($"Map v{version} is published");
        if (matching.Count > 0)
        {
            line += "; the code index matches for " + string.Join(", ", matching);
        }

        return line + (missing.Count > 0 ? "; not indexed yet: " + string.Join(", ", missing) : string.Empty) + ".";
    }

    // At most two repos' lines, then "and N more".
    private static List<string> Capped(IEnumerable<string> lines)
    {
        var all = lines.ToList();
        return all.Count <= LinesShown ? all : [.. all.Take(LinesShown), Text($"and {all.Count - LinesShown} more")];
    }

    // The reasons that mean the map did not wait for the index; a map that waited (or timed out waiting) reads published.
    private static string? WhyNotWaited(string? reason) => reason switch
    {
        "FirstMap" => "it is the first map",
        "WaitOff" => "waiting is turned off",
        "IndexBroken" => "the code index is broken",
        _ => null,
    };

    private static string WaitWord(RepoWait word) => word switch
    {
        RepoWait.Queued => "build queued",
        RepoWait.Building => "building",
        RepoWait.WaitingForSlot => "waiting for a free build slot",
        RepoWait.Retrying => "retrying",
        RepoWait.NewerIndex => "the index is at a newer commit",
        RepoWait.Damaged => "index damaged, rebuilding",
        RepoWait.NotIndexed => "not indexed yet",
        RepoWait.None => "waiting",
        _ => "waiting",
    };
}
