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
