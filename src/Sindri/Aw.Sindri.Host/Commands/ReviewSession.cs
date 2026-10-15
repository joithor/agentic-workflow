using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Review;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The interactive loop of 'context map review' as a state machine over the queue (the TTY shell only reads lines and sends what it
/// says): first the bulk accept of the deterministic proposals, then each item with accept, reject, rename, move, skip or quit;
/// orphaned overrides are dropped or repointed. A skip writes nothing (README X63), and quitting keeps what was decided.
/// </summary>
/// <param name="queue">The queue as the server listed it.</param>
internal sealed class ReviewSession(ReviewQueueView queue)
{
    private List<ReviewEntry> _items = [];
    private Stage _stage = Stage.Done;
    private int _index;
    private (int Accepted, int Rejected, int Edited, int Skipped, int Dropped) _counts;
    private string _pending = string.Empty;
    private bool _bulkAccepted;

    private enum Stage
    {
        Bulk,
        Item,
        AskLabel,
        AskParent,
        AskTarget,
        Done,
    }

    /// <summary>How many items each action decided or passed over.</summary>
    internal ReviewCounts Counts => new(_counts.Accepted, _counts.Rejected, _counts.Edited, _counts.Skipped, _counts.Dropped);

    /// <summary>
    /// How many undecided items the queue left out (the server cuts each group): they are not walked here, and the shell says so.
    /// The deterministic ones are not missed when the bulk accept took the whole backlog.
    /// </summary>
    internal int Hidden =>
        Math.Max(0, queue.Counts.Model - queue.Entries.Count(e => e.Group == ReviewGroup.Model))
        + Math.Max(0, queue.Counts.Orphaned - queue.Entries.Count(e => e.Group == ReviewGroup.Orphaned))
        + (_bulkAccepted ? 0 : Math.Max(0, queue.Counts.Deterministic - queue.Entries.Count(e => e.Group == ReviewGroup.Deterministic)));

    /// <summary>The server took <paramref name="step"/>'s request: it counts now, and an item renamed or moved shows its new name or parent.</summary>
    /// <param name="step">The step that was sent.</param>
    /// <param name="written">How many rows the server wrote for a bulk accept (the count is what it wrote, not what was listed), or null when it didn't say.</param>
    /// <returns>The step to go on with.</returns>
    internal ReviewStep Applied(ReviewStep step, int? written = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Tally is not { } tally)
        {
            return step;
        }

        if (IsBulk(step))
        {
            _bulkAccepted = true;   // only now: the shown deterministic items were left out of the walk and the cut ones are no longer waiting
        }

        switch (tally.Outcome)
        {
            case ReviewOutcome.Accepted:
                _counts.Accepted += IsBulk(step) ? written ?? tally.Count : tally.Count;
                break;
            case ReviewOutcome.Rejected:
                _counts.Rejected += tally.Count;
                break;
            case ReviewOutcome.Dropped:
                _counts.Dropped += tally.Count;
                break;
            case ReviewOutcome.Edited:
                _counts.Edited += tally.Count;
                return Renamed(step, tally);
            default:
                break;
        }

        return step;
    }

    private ReviewStep Renamed(ReviewStep step, ReviewTally tally)
    {
        _items[_index] = _items[_index] with
        {
            Label = tally.Label ?? _items[_index].Label,
            Parent = tally.Parent is { } parent && NodeKey.TryFrom(parent, out var key) ? key : _items[_index].Parent,
        };
        return step with { Prompt = ItemPrompt() };
    }

    /// <summary>The server refused <paramref name="step"/>'s request: nothing it would have counted moved. A refused bulk accept puts the deterministic items back into the walk.</summary>
    /// <param name="step">The step that was sent.</param>
    /// <returns>The step to go on with.</returns>
    internal ReviewStep Failed(ReviewStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!IsBulk(step))
        {
            // A refused decision moved the cursor past its item: back to it, neither decided nor skipped. A rename or a move never left it.
            if (step.Tally is { Outcome: not ReviewOutcome.Edited })
            {
                _index--;
                _stage = Stage.Item;
                return new ReviewStep(ItemPrompt());
            }

            return step;
        }

        _bulkAccepted = false;
        _items = [.. queue.Entries];
        _index = -1;
        return Advance(send: null);
    }

    private static bool IsBulk(ReviewStep step) => step.Send is { } send && string.Equals(send.Verb, "accept-deterministic", StringComparison.Ordinal);

    private static bool Bulkable(ReviewEntry e) => e is { Group: ReviewGroup.Deterministic, Link: null };

    /// <summary>The first prompt.</summary>
    /// <returns>The step.</returns>
    internal ReviewStep Start()
    {
        var bulk = queue.Counts.Deterministic;
        if (bulk == 0)
        {
            return Begin([.. queue.Entries]);
        }

        _stage = Stage.Bulk;
        return new ReviewStep(string.Create(CultureInfo.InvariantCulture, $"Accept all {bulk} deterministic proposals at once? [y]es / [n]o, go through them one by one / [q]uit: "));
    }

    /// <summary>The human typed <paramref name="line"/>.</summary>
    /// <param name="line">The typed line.</param>
    /// <returns>The step.</returns>
    internal ReviewStep Answer(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Trim();
        return _stage switch
        {
            Stage.Bulk => AnswerBulk(text),
            Stage.Item => _items[_index].Group == ReviewGroup.Orphaned ? AnswerOrphan(text) : AnswerItem(text),
            Stage.AskLabel or Stage.AskParent or Stage.AskTarget => AnswerValue(text),
            Stage.Done => new ReviewStep(Prompt: null),
            _ => new ReviewStep(Prompt: null),
        };
    }

    private ReviewStep AnswerBulk(string text) => text.ToLowerInvariant() switch
    {
        "y" or "yes" => Begin([.. queue.Entries.Where(e => !Bulkable(e))], new ReviewContextMapRequest("accept-deterministic"), new ReviewTally(ReviewOutcome.Accepted, queue.Counts.Deterministic)),
        "n" or "no" => Begin([.. queue.Entries]),
        "q" or "quit" => Finish(),
        _ => Again(Start().Prompt),
    };

    private ReviewStep AnswerItem(string text)
    {
        var entry = _items[_index];
        var target = Target(entry);
        switch (text.ToLowerInvariant())
        {
            case "a":
                return Advance(target("accept"), new ReviewTally(ReviewOutcome.Accepted));
            case "r":
                return Advance(target("reject"), new ReviewTally(ReviewOutcome.Rejected));
            case "s":
                _counts.Skipped++;
                return Advance(send: null);
            case "q":
                return Finish();
            case "n" when entry.Link is null:
                return Ask(Stage.AskLabel, "New name: ");
            case "m" when entry.Link is null:
                return Ask(Stage.AskParent, "Move under which node key? ");
            default:
                return Again(ItemPrompt());
        }
    }

    private ReviewStep AnswerOrphan(string text)
    {
        var entry = _items[_index];
        switch (text.ToLowerInvariant())
        {
            case "d":
                return Advance(new ReviewContextMapRequest("drop", Override: entry.Override?.Value), new ReviewTally(ReviewOutcome.Dropped));
            case "p":
                return Ask(Stage.AskTarget, "Point it at which node key? ");
            case "s":
                _counts.Skipped++;
                return Advance(send: null);
            case "q":
                return Finish();
            default:
                return Again(ItemPrompt());
        }
    }

    private ReviewStep AnswerValue(string text)
    {
        if (text.Length == 0)
        {
            _stage = Stage.Item;
            return Again(ItemPrompt());
        }

        var entry = _items[_index];
        return _stage switch
        {
            Stage.AskLabel => Stay(new ReviewContextMapRequest("rename", Node: entry.Node.Value, Label: text), new ReviewTally(ReviewOutcome.Edited, Label: text)),
            Stage.AskParent => Stay(new ReviewContextMapRequest("move", Node: entry.Node.Value, Other: text), new ReviewTally(ReviewOutcome.Edited, Parent: text)),
            Stage.AskTarget => Advance(new ReviewContextMapRequest("repoint", Node: text, Override: entry.Override?.Value), new ReviewTally(ReviewOutcome.Dropped)),
            Stage.Bulk or Stage.Item or Stage.Done => Finish(),
            _ => Finish(),
        };
    }

    private static Func<string, ReviewContextMapRequest> Target(ReviewEntry entry) => verb =>
        entry.Link is { } link ? new ReviewContextMapRequest(verb, Link: link.Value) : new ReviewContextMapRequest(verb, Node: entry.Node.Value);

    private ReviewStep Begin(List<ReviewEntry> items, ReviewContextMapRequest? send = null, ReviewTally? tally = null)
    {
        _items = items;
        _index = -1;
        return Advance(send, tally);
    }

    private ReviewStep Advance(ReviewContextMapRequest? send, ReviewTally? tally = null)
    {
        _index++;
        _stage = Stage.Item;
        if (_index >= _items.Count)
        {
            _stage = Stage.Done;
            return new ReviewStep(Prompt: null, send, tally);
        }

        return new ReviewStep(ItemPrompt(), send, tally);
    }

    // A rename or a move doesn't decide: the item stays current, and shows its new value once the server has taken the change.
    private ReviewStep Stay(ReviewContextMapRequest send, ReviewTally tally)
    {
        _stage = Stage.Item;
        return new ReviewStep(ItemPrompt(), send, tally);
    }

    private ReviewStep Finish()
    {
        _stage = Stage.Done;
        return new ReviewStep(Prompt: null);
    }

    private ReviewStep Ask(Stage stage, string prompt)
    {
        _stage = stage;
        _pending = prompt;
        return new ReviewStep(_pending);
    }

    private static ReviewStep Again(string? prompt) => new(prompt);

    private string ItemPrompt()
    {
        var entry = _items[_index];
        var files = entry.Files.Count == 0 ? string.Empty : " · " + string.Join(", ", entry.Files.Take(3).Select(f => f.Value));
        var head = string.Create(CultureInfo.InvariantCulture,
            $"[{_index + 1}/{_items.Count}] {entry.Label} · {entry.Group.ToString().ToLowerInvariant()} {entry.Confidence:0.00} · {entry.Node.Value}{files}\n");
        return head + (entry.Group == ReviewGroup.Orphaned
            ? "  [d]rop it / [p]oint it at another node / [s]kip / [q]uit: "
            : entry.Link is null ? "  [a]ccept / [r]eject / re[n]ame / [m]ove / [s]kip / [q]uit: " : "  [a]ccept / [r]eject / [s]kip / [q]uit: ");
    }
}
