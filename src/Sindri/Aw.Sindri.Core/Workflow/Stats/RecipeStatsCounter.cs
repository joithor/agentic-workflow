using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// Counts a recipe's stats from its workflow ledger rows (API spec §3.8, E-24): a pure function of the rows, the window and the clock. A run is an
/// instance whose start row names the recipe; it counts when it started inside the window, and its later rows count wherever they fall. A park, an
/// escalation or a takeover begins when a row's suspension kind changes to it and ends when it changes again; a park answered or an escalation decided
/// is one that a human's answer ended, and a run with any of those or a takeover needed a person once, however many it had.
/// </summary>
public static class RecipeStatsCounter
{
    private const string Scheme = "sha256:";
    private const int Weeks = 4;

    /// <summary>The stats of <paramref name="recipe"/> for the runs that started inside <paramref name="window"/> before <paramref name="now"/>.</summary>
    /// <param name="events">The workflow rows, in ledger order.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hashPrefix">A recipe hash (in full or a prefix of 8 or more digits) to count only runs under it; null for every version.</param>
    /// <param name="window">The window.</param>
    /// <param name="now">The read's time.</param>
    /// <returns>The stats.</returns>
    public static RecipeStats Count(IReadOnlyList<StatEvent> events, RecipeId recipe, string? hashPrefix, StatsWindow window, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(window);
        var since = now - window.Span;
        var tally = new Tally();
        var runs = new List<Run>();
        foreach (var rows in events.GroupBy(static e => e.Address, StringComparer.Ordinal))
        {
            var start = rows.FirstOrDefault(static e => e.RecipeId is not null);
            if (start is null || !string.Equals(start.RecipeId, recipe.Value, StringComparison.Ordinal) || start.At < since || !InVersion(start.RecipeHash, hashPrefix))
            {
                continue;
            }

            runs.Add(Walk(rows, start, tally));
        }

        return Stats(recipe, hashPrefix, window, now, runs, tally);
    }

    /// <summary>Whether any root instance (an address of three segments) of <paramref name="recipe"/> ever started.</summary>
    /// <param name="events">The workflow rows.</param>
    /// <param name="recipe">The recipe.</param>
    /// <returns>True when one did.</returns>
    public static bool HasRoot(IReadOnlyList<StatEvent> events, RecipeId recipe)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Any(e => string.Equals(e.RecipeId, recipe.Value, StringComparison.Ordinal) && e.Address.Count(static c => c == '/') == 2);
    }

    private static bool InVersion(string? hash, string? prefix)
    {
        if (prefix is null)
        {
            return true;
        }

        var wanted = prefix.StartsWith(Scheme, StringComparison.Ordinal) ? prefix[Scheme.Length..] : prefix;
        return RecipeHash.IsPrefix(prefix) && hash is not null && hash.StartsWith(Scheme, StringComparison.Ordinal) && hash[Scheme.Length..].StartsWith(wanted, StringComparison.Ordinal);
    }

    private static Run Walk(IEnumerable<StatEvent> rows, StatEvent start, Tally tally)
    {
        var run = new Run(start.At);
        var state = string.Empty;
        var entered = start.At;
        Open? open = null;
        foreach (var row in rows)
        {
            foreach (var gate in row.Gates)
            {
                var figures = tally.Row(gate.Gate, isGate: true);
                figures.Evaluated++;
                figures.Refused += gate.Outcome is GateOutcome.Refused or GateOutcome.Unavailable ? 1 : 0;
            }

            if (row.HasDetail && open?.Kind != row.Suspension)
            {
                if (open is not null)
                {
                    End(open, row, run, tally);
                }

                open = row.Suspension is { } kind ? Begin(kind, row, state, run, tally) : null;
            }

            if (row.To.Length > 0 && !EngineStates.IsEngineOwned(row.To) && !string.Equals(row.To, state, StringComparison.Ordinal))
            {
                if (state.Length > 0)
                {
                    tally.Row(state, isGate: false).Seconds.Add((row.At - entered).TotalSeconds);
                }

                (state, entered) = (row.To, row.At);
            }

            run.Tokens = row.TokensSpent is { } spent ? Math.Max(run.Tokens ?? 0, spent) : run.Tokens;
            if (row.Outcome is { } outcome)
            {
                (run.Outcome, run.EndedAt) = (outcome, row.At);
            }
        }

        return run;
    }

    private static Open Begin(SuspensionKind kind, StatEvent row, string state, Run run, Tally tally)
    {
        switch (kind)
        {
            case SuspensionKind.Park:
                tally.Row(state, isGate: false).Parks.Increment(ParkText(row.ParkReason));
                tally.BudgetHits += row.ParkReason == ParkReason.Budget ? 1 : 0;
                break;
            case SuspensionKind.Escalation:
                var escalated = row.Gates.Where(static g => g.Outcome == GateOutcome.Escalated).ToList();
                if (escalated.Count == 0)
                {
                    tally.Row(state, isGate: false).Escalations.Increment(EscalationText(row.Escalation));
                }

                foreach (var gate in escalated)
                {
                    tally.Row(gate.Gate, isGate: true).Escalations.Increment(gate.Threshold is { } t ? string.Create(CultureInfo.InvariantCulture, $"below {t:0.00}") : EscalationText(row.Escalation));
                }

                break;
            case SuspensionKind.Takeover:
                tally.Row(state, isGate: false).Takeovers++;
                run.Takeover = true;
                break;
            default:
                break;
        }

        return new Open(kind, row.At);
    }

    private static void End(Open open, StatEvent row, Run run, Tally tally)
    {
        var seconds = (row.At - open.StartedAt).TotalSeconds;
        switch (open.Kind)
        {
            case SuspensionKind.Park when row.Answered:
                tally.ParkSeconds.Add(seconds);
                run.ParkAnswered = true;
                break;
            case SuspensionKind.Escalation when row.Answered:
                tally.EscalationSeconds.Add(seconds);
                run.EscalationDecided = true;
                break;
            case SuspensionKind.Takeover:
                tally.TakeoverSeconds.Add(seconds);
                break;
            case SuspensionKind.Park:
                break;
            case SuspensionKind.Escalation:
                break;
            default:
                break;
        }
    }

    private static RecipeStats Stats(RecipeId recipe, string? hash, StatsWindow window, DateTimeOffset now, List<Run> runs, Tally tally)
    {
        var done = runs.Where(static r => r.Class == Ending.Done).ToList();
        var tokens = runs.Where(static r => r.Tokens is > 0).Select(static r => (double)r.Tokens!.Value).Order().ToList();   // why: the filter keeps only runs with a spend
        return new RecipeStats(
            recipe.Value,
            hash,
            window.Text,
            ChildOnly: false,
            runs.Count,
            done.Count,
            runs.Count(static r => r.Class == Ending.Failed),
            runs.Count(static r => r.Class == Ending.Cancelled),
            new StatsOutcomes(done.Count(static r => !r.NeededHuman), done.Count(static r => !r.Takeover && r.NeededHuman), done.Count(static r => r.Takeover)),
            runs.Count == 0 ? null : (double)runs.Count(static r => r.NeededHuman) / runs.Count,
            Median(runs.Where(static r => r.EndedAt is not null).Select(static r => (r.EndedAt!.Value - r.StartedAt).TotalSeconds)),   // why: the filter keeps only ended runs
            new StatsTokens(Rank(tokens, 0.5), Rank(tokens, 0.9)),
            tally.BudgetHits,
            new StatsHumanTime(Figure(tally.ParkSeconds), Figure(tally.EscalationSeconds), Figure(tally.TakeoverSeconds)),
            [.. tally.Rows.Select(static r => r.Output())],
            Buckets(runs, now));
    }

    private static List<StatsBucket> Buckets(List<Run> runs, DateTimeOffset now)
    {
        var today = now.UtcDateTime.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var buckets = new List<StatsBucket>(Weeks);
        for (var week = Weeks - 1; week >= 0; week--)
        {
            var from = monday.AddDays(-7 * week);
            var inWeek = runs.Where(r => r.StartedAt.UtcDateTime >= from && r.StartedAt.UtcDateTime < from.AddDays(7)).ToList();
            buckets.Add(new StatsBucket(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), inWeek.Count, inWeek.Count == 0 ? null : (double)inWeek.Count(static r => r.NeededHuman) / inWeek.Count));
        }

        return buckets;
    }

    private static HumanTimeFigure Figure(List<double> seconds) => new(seconds.Count, Median(seconds));

    private static long? Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? null : (long)Math.Round(sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2, MidpointRounding.AwayFromZero);
    }

    // nearest rank: the smallest value that at least p of the values are at or below
    private static long? Rank(List<double> sorted, double p) => sorted.Count == 0 ? null : (long)sorted[Math.Max((int)Math.Ceiling(p * sorted.Count), 1) - 1];

    private static string ParkText(ParkReason? reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime => "SND-WF-010",
        _ => "unknown",
    };

    private static string EscalationText(EscalationKind? kind) => kind switch
    {
        EscalationKind.Gate => "gate",
        EscalationKind.Timeout => "timeout",
        EscalationKind.Child => "child",
        _ => "unknown",
    };

    private static void Increment(this Dictionary<string, int> counts, string reason) => counts[reason] = counts.GetValueOrDefault(reason) + 1;

    private enum Ending
    {
        Open,
        Done,
        Failed,
        Cancelled,
    }

    private sealed record Open(SuspensionKind Kind, DateTimeOffset StartedAt);

    private sealed class Run(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; } = startedAt;

        public DateTimeOffset? EndedAt { get; set; }

        public string? Outcome { get; set; }

        public int? Tokens { get; set; }

        public bool ParkAnswered { get; set; }

        public bool EscalationDecided { get; set; }

        public bool Takeover { get; set; }

        public bool NeededHuman => ParkAnswered || EscalationDecided || Takeover;

        public Ending Class => Outcome switch
        {
            null => Ending.Open,
            "resolved" or "alreadySatisfied" => Ending.Done,
            "cancelled" => Ending.Cancelled,
            _ => Ending.Failed,
        };
    }

    private sealed class Tally
    {
        private readonly Dictionary<string, Figures> _rows = new(StringComparer.Ordinal);

        public IEnumerable<Figures> Rows => _rows.Values;

        public List<double> ParkSeconds { get; } = [];

        public List<double> EscalationSeconds { get; } = [];

        public List<double> TakeoverSeconds { get; } = [];

        public int BudgetHits { get; set; }

        public Figures Row(string name, bool isGate)
        {
            if (!_rows.TryGetValue(name, out var row))
            {
                _rows[name] = row = new Figures(name, isGate);
            }

            return row;
        }
    }

    private sealed class Figures(string name, bool isGate)
    {
        public List<double> Seconds { get; } = [];

        public Dictionary<string, int> Parks { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> Escalations { get; } = new(StringComparer.Ordinal);

        public int Takeovers { get; set; }

        public int Refused { get; set; }

        public int Evaluated { get; set; }

        public StateStats Output() => new(name, isGate ? null : Median(Seconds), Reasons(Parks), Reasons(Escalations), Takeovers, isGate ? new GateRefusals(Refused, Evaluated) : null);

        private static List<ReasonCount> Reasons(Dictionary<string, int> counts) =>
            [.. counts.OrderByDescending(static c => c.Value).ThenBy(static c => c.Key, StringComparer.Ordinal).Select(static c => new ReasonCount(c.Key, c.Value))];
    }
}
