using System.Globalization;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

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
    /// <param name="zone">The owner's time zone, for the times in the sentences.</param>
    /// <returns>The state, its words and the stale repos.</returns>
    internal static MapPublishStatusView Of(MapPublishFacts facts, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(zone);
        if (facts.HeldVersion is { } held)
        {
            return HeldView(facts, held, now, zone);
        }

        var view = Settled(facts, now);
        return facts.DroppedVersion is { } dropped
            ? view with { Lines = [Text($"Held map v{dropped} was replaced by a newer build."), .. view.Lines] }
            : view;
    }

    private static MapPublishStatusView Settled(MapPublishFacts facts, DateTimeOffset now)
    {
        var stale = StaleOf(facts.Repos);
        var version = facts.Current ?? 0;
        if (stale.Count > 0)
        {
            return Plain(PublishedStale, "published, code answers stale", facts, stale, StaleLines(version, facts, stale, now));
        }

        if (facts.ByHand)
        {
            return Plain(PublishedByHand, "published by hand", facts, [], [Text($"Map v{version} was published by hand.")]);
        }

        return WithoutWaiting(facts.Reason, version) is { } line
            ? Plain(PublishedWithoutWaiting, "published without waiting", facts, [], [line])
            : Plain(Published, "published", facts, [], [facts.Reason == "TimedOut" ? Text($"Map v{version} waited out its wait; the code index has caught up.") : PublishedLine(version, facts.Repos)]);
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

    private static MapPublishStatusView HeldView(MapPublishFacts facts, int held, DateTimeOffset now, TimeZoneInfo zone)
    {
        var until = (facts.Since < now ? facts.Since : now) + facts.Wait;
        var overdue = now > until + _overdueAfter;
        var at = TimeZoneInfo.ConvertTime(until, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
        var waiting = facts.Repos.Where(static r => r.Waiting).ToList();
        var current = facts.Current is { } v ? Text($"v{v} is current") : "there is no current map";
        List<string> lines = [HeldLead(held, overdue, (at, current), waiting, now), .. HeldNotes(facts.Repos)];
        if (!overdue && waiting.Count > 0)
        {
            lines.Add($"It goes ahead by about {at} (your time) unless a newer build replaces it; {current} until then. To publish now: sindri context map promote.");
        }

        return new MapPublishStatusView(overdue ? Overdue : Held, overdue ? "overdue" : "held for the code index", Reason: null, Stale: false, [], held, until, lines, PublishedAt: null, [.. waiting.Select(r => $"{r.Repo} ({WaitWord(r, now)})")]);
    }

    private static string HeldLead(int held, bool overdue, (string At, string Current) words, List<PublishRepoFact> waiting, DateTimeOffset now)
    {
        if (overdue)
        {
            return Text($"Map v{held} should have gone ahead at {words.At}. The map service is paused, or publishing is failing. {words.Current}. Resume the map service or see the failing job; or sindri context map promote.");
        }

        if (waiting.Count == 0)
        {
            return Text($"Map v{held} is held; none of your repos are waiting.");
        }

        var ready = Text($"Map v{held} is ready. Waiting for the code index of ");
        return ready + Listed(waiting.Select(r => $"{r.Repo} ({WaitWord(r, now)})")) + ".";
    }

    // The repos a held map does not wait on: not indexed at all, or already at a newer commit.
    private static List<string> HeldNotes(IReadOnlyList<PublishRepoFact> repos)
    {
        var absent = repos.Where(static r => !r.Waiting && r.Word == RepoWait.NotIndexed).Select(static r => r.Repo).ToList();
        List<string> notes = [];
        if (absent.Count > 0)
        {
            notes.Add(Listed(absent) + (absent.Count == 1 ? " is" : " are") + " not indexed yet and " + (absent.Count == 1 ? "is" : "are") + " not waited for.");
        }

        notes.AddRange(repos.Where(static r => !r.Waiting && r.Word == RepoWait.NewerIndex).Select(static r => $"Code answers for {r.Repo} are already from the newer commit."));
        return notes;
    }

    // At most two repos named, then "and N more".
    private static string Listed(IEnumerable<string> items)
    {
        var all = items.ToList();
        if (all.Count <= LinesShown)
        {
            return string.Join(", ", all);
        }

        var more = Text($" and {all.Count - LinesShown} more");
        return string.Join(", ", all.Take(LinesShown)) + more;
    }

    private static MapPublishStatusView Plain(string state, string word, MapPublishFacts facts, List<StaleRepo> stale, IReadOnlyList<string> lines) =>
        new(state, word, facts.Reason, stale.Count > 0, stale, facts.Current ?? 0, Until: null, lines, facts.PublishedAt);

    private static List<StaleRepo> StaleOf(IReadOnlyList<PublishRepoFact> repos) =>
        [.. repos.Where(static r => r.MapCommit is not null && r.IndexCommit is not null && !Same(r.MapCommit, r.IndexCommit))
            .Select(static r => new StaleRepo(r.Repo, Short(r.IndexCommit ?? string.Empty), Short(r.MapCommit ?? string.Empty)))];

    // Heads are stored as 7 to 40 hex characters and the index may name the full commit: the shorter is a prefix of the longer.
    private static bool Same(string a, string b) => a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal);

    private static List<string> StaleLines(int version, MapPublishFacts facts, List<StaleRepo> stale, DateTimeOffset now)
    {
        var several = facts.Repos.Count > 1;
        var relation = facts.Reason switch
        {
            "IndexReady" => "newer than",
            "TimedOut" or "FirstMap" or "WaitOff" or "IndexBroken" => "older than",
            _ => "not the commit of",
        };
        var lines = Capped(stale.Select(s => Text($"{(several ? s.Repo + ": " : string.Empty)}Code answers are from {s.IndexCommit} ({Built(facts, s, now)}{relation} map v{version}, which is for {s.MapCommit}).")));
        return facts.Reason switch
        {
            "TimedOut" => [.. lines, "The code index did not finish in time."],
            "IndexBroken" => [.. lines, "The code index is broken, see Code index."],
            _ => lines,
        };
    }

    // "built 12 min ago, " when the port said when the index was built.
    private static string Built(MapPublishFacts facts, StaleRepo stale, DateTimeOffset now) =>
        facts.Repos.FirstOrDefault(r => r.Repo == stale.Repo)?.BuiltAt is { } at ? Text($"built {Math.Max(0, (int)(now - at).TotalMinutes)} min ago, ") : string.Empty;

    private static string PublishedLine(int version, IReadOnlyList<PublishRepoFact> repos)
    {
        var matching = repos.Where(static r => r.IndexCommit is not null).ToList();
        var missing = repos.Where(static r => r.IndexCommit is null).Select(static r => r.Repo).ToList();
        var line = Text($"Map v{version} is current");
        if (matching.Count > 0)
        {
            line += "; the code index matches" + (repos.Count > 1 ? " for " + string.Join(", ", matching.Select(static r => r.Repo)) : string.Empty) + " (" + string.Join(", ", matching.Select(r => repos.Count > 1 ? $"{r.Repo} {Short(r.IndexCommit ?? string.Empty)}" : Short(r.IndexCommit ?? string.Empty))) + ")";
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
    private static string? WithoutWaiting(string? reason, int version) => reason switch
    {
        "FirstMap" => Text($"Map v{version} is the first map; the code index is not part of it yet."),
        "NoIndex" => Text($"Map v{version} is current; there is no code index to wait for."),
        "WaitOff" => Text($"Map v{version} is current; waiting for the code index is turned off."),
        "IndexBroken" => Text($"Map v{version} is current; the code index was broken when it was published."),
        _ => null,
    };

    private static string WaitWord(PublishRepoFact repo, DateTimeOffset now) => repo.Word switch
    {
        RepoWait.Queued => "build queued",
        RepoWait.Building => "building",
        RepoWait.WaitingForSlot => "waiting for a free build slot",
        RepoWait.Retrying => repo.RetryAt is { } at ? Text($"retrying in {Math.Max(1, (int)Math.Ceiling((at - now).TotalMinutes))} min") : "retrying",
        RepoWait.NewerIndex => "index is newer (waiting for the next map)",
        RepoWait.Damaged => "index damaged (rebuilding)",
        RepoWait.NotIndexed => "not indexed yet",
        RepoWait.None => "waiting",
        _ => "waiting",
    };
}
