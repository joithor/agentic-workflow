using System.Globalization;

namespace Aw.Meta.Core.Gate;

/// <summary>The text forms of a train: the summary table the command prints and the status shows.</summary>
public static class TrainText
{
    /// <summary>The base column: "main", or "main" plus the pull requests merged under it.</summary>
    /// <param name="stack">The pull requests ahead.</param>
    /// <returns>The text.</returns>
    public static string BaseOf(IReadOnlyList<StackedPr> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return stack.Count == 0 ? "main" : "main+" + string.Join(',', stack.Select(s => string.Create(CultureInfo.InvariantCulture, $"#{s.Pr}")));
    }

    /// <summary>Marks a stored train board whose process died: a board with a "TRAIN pid N" header and no "TRAIN ended" line gets a final stale line when pid N is not running.</summary>
    /// <param name="board">The stored board text, or null.</param>
    /// <param name="isRunning">Whether a pid is a running process.</param>
    /// <returns>The board, with the stale line when it applies.</returns>
    public static string? MarkStale(string? board, Func<int, bool> isRunning)
    {
        ArgumentNullException.ThrowIfNull(isRunning);
        const string Header = "TRAIN pid ";
        if (board is null || !board.StartsWith(Header, StringComparison.Ordinal) || board.Contains("\nTRAIN ended", StringComparison.Ordinal))
        {
            return board;
        }

        var digits = new string([.. board[Header.Length..].TakeWhile(char.IsAsciiDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && !isRunning(pid)
            ? board + string.Create(CultureInfo.InvariantCulture, $"\nTRAIN stale: process {pid} is gone and wrote no end line")
            : board;
    }

    /// <summary>The TRAIN summary: a header, then one line per pull request in train order (left-out ones after).</summary>
    /// <param name="main">The main sha the train is pinned to.</param>
    /// <param name="entries">The pull requests.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Summary(string main, IReadOnlyList<TrainEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = entries.OrderBy(e => e.Position == 0 ? int.MaxValue : e.Position).ThenBy(e => e.Pr).ToList();
        var width = ordered.Select(e => e.Base.Length).DefaultIfEmpty().Max();
        return
        [
            $"TRAIN main {(main.Length > 7 ? main[..7] : main)}: position, PR, base, RESULT",
            .. ordered.Select(e => string.Create(
                CultureInfo.InvariantCulture,
                $"  {(e.Position == 0 ? "-" : e.Position.ToString(CultureInfo.InvariantCulture))}  #{e.Pr}  {e.Base.PadRight(width)}  {e.State.ToString().ToUpperInvariant()}{(e.Note.Length > 0 ? " " + e.Note : string.Empty)}")),
        ];
    }
}
