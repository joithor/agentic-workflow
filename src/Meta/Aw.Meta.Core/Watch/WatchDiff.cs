using System.Globalization;

namespace Aw.Meta.Core.Watch;

/// <summary>Turns successive polls into one line per settled change.</summary>
public static class WatchDiff
{
    private const string Unknown = "UNKNOWN";
    private const int SettlePolls = 2;
    private const int ShortLength = 7;

    /// <summary>Compares a poll with what the watch remembers.</summary>
    /// <param name="state">The state after the previous poll.</param>
    /// <param name="snapshot">This poll.</param>
    /// <returns>The lines to report and the new state. The first poll only sets the baseline.</returns>
    public static WatchStep Step(WatchState state, WatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);
        var lines = new List<string>();
        var prs = new Dictionary<int, WatchedPr>(state.Prs);
        foreach (var pr in snapshot.Prs)
        {
            prs[pr.Number] = prs.TryGetValue(pr.Number, out var known) ? Advance(known, pr, lines) : Fresh(pr, state.Initialized, lines);
        }

        if (state.Initialized && state.MainSha is not null && state.MainSha != snapshot.MainSha)
        {
            lines.Add("MAIN moved " + Short(snapshot.MainSha));
        }

        var (disk, diskSeen) = Settle(state.DiskLow, snapshot.DiskLow, state.DiskFlipSeen, "DISK", lines);
        var (memory, memorySeen) = Settle(state.MemoryLow, snapshot.MemoryLow, state.MemoryFlipSeen, "MEMORY", lines);
        return new WatchStep(lines, new WatchState(Initialized: true, prs, snapshot.MainSha, disk, memory, diskSeen, memorySeen));
    }

    // A resource flips once the poll disagrees with the reported state twice in a row, so a host sitting at its floor does not
    // flap LOW, OK, LOW (each line tells every lane to pause or resume).
    private static (bool Reported, bool FlipSeen) Settle(bool reported, bool seen, bool flipSeen, string name, List<string> lines)
    {
        if (seen == reported)
        {
            return (reported, false);
        }

        if (!flipSeen)
        {
            return (reported, true);
        }

        lines.Add(name + (seen ? " LOW" : " OK"));
        return (seen, false);
    }

    private static WatchedPr Fresh(PrView pr, bool report, List<string> lines)
    {
        if (report)
        {
            lines.Add(Line(pr.Number, Reported(pr.State)));
        }

        return new WatchedPr(pr.State, pr.IsDraft, pr.Head, pr.Mergeable == Unknown ? null : pr.Mergeable, Pending: null, PendingPolls: 0);
    }

    private static WatchedPr Advance(WatchedPr known, PrView pr, List<string> lines)
    {
        if (known.State != pr.State)
        {
            lines.Add(Line(pr.Number, Reported(pr.State)));
        }
        else if (known.IsDraft != pr.IsDraft)
        {
            lines.Add(Line(pr.Number, pr.IsDraft ? "draft" : "ready"));
        }

        if (known.Head != pr.Head)
        {
            lines.Add(Line(pr.Number, "head " + Short(pr.Head)));
        }

        var moved = known with { State = pr.State, IsDraft = pr.IsDraft, Head = pr.Head };
        return pr.State == PrState.Open ? Settle(moved, pr, lines) : moved;
    }

    // A flip is real once it holds for two polls; UNKNOWN says nothing and changes nothing.
    private static WatchedPr Settle(WatchedPr known, PrView pr, List<string> lines)
    {
        var value = pr.Mergeable;
        if (value == Unknown)
        {
            return known;
        }

        if (known.Mergeable is null || value == known.Mergeable)
        {
            return known with { Mergeable = known.Mergeable ?? value, Pending = null, PendingPolls = 0 };
        }

        var polls = known.Pending == value ? known.PendingPolls + 1 : 1;
        if (polls < SettlePolls)
        {
            return known with { Pending = value, PendingPolls = polls };
        }

        lines.Add(Line(pr.Number, "mergeable " + value));
        return known with { Mergeable = value, Pending = null, PendingPolls = 0 };
    }

    private static string Reported(PrState state) => state switch
    {
        PrState.Open => "opened",
        PrState.Closed => "closed",
        PrState.Merged => "merged",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "unknown pull request state"),
    };

    private static string Line(int number, string text) => "PR #" + number.ToString(CultureInfo.InvariantCulture) + " " + text;

    private static string Short(string sha) => sha.Length <= ShortLength ? sha : sha[..ShortLength];
}
