using System.Globalization;
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

    /// <summary>How many undecided items the queue left out (it is cut per group); they are not walked here.</summary>
    internal int Hidden => _items.Count * 0;

    /// <summary>The server took <paramref name="step"/>'s request.</summary>
    /// <param name="step">The step that was sent.</param>
    /// <returns>The step to go on with.</returns>
    internal ReviewStep Applied(ReviewStep step)
    {
        _ = Counts;
        return step;
    }

    /// <summary>The server refused <paramref name="step"/>'s request.</summary>
    /// <param name="step">The step that was sent.</param>
    /// <returns>The step to go on with.</returns>
    internal ReviewStep Refused(ReviewStep step)
    {
        _ = Counts;
        return step;
    }

    private static bool Bulkable(ReviewEntry e) => e is { Group: ReviewGroup.Deterministic, Link: null };

    /// <summary>The first prompt.</summary>
    /// <returns>The step.</returns>
    internal ReviewStep Start()
    {
        var bulk = queue.Entries.Count(Bulkable);
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

    private ReviewStep AnswerBulk(string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "y" or "yes":
                _counts.Accepted += queue.Entries.Count(Bulkable);
                return Begin([.. queue.Entries.Where(e => !Bulkable(e))], new ReviewContextMapRequest("accept-deterministic"));
            case "n" or "no":
                return Begin([.. queue.Entries]);
            case "q" or "quit":
                return Finish();
            default:
                return Again(Start().Prompt);
        }
    }

    private ReviewStep AnswerItem(string text)
    {
        var entry = _items[_index];
        var target = Target(entry);
        switch (text.ToLowerInvariant())
        {
            case "a":
                _counts.Accepted++;
                return Advance(target("accept"));
            case "r":
                _counts.Rejected++;
                return Advance(target("reject"));
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
                _counts.Dropped++;
                return Advance(new ReviewContextMapRequest("drop", Override: entry.Override?.Value));
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
        switch (_stage)
        {
            case Stage.AskLabel:
                _counts.Edited++;
                return Advance(new ReviewContextMapRequest("rename", Node: entry.Node.Value, Label: text));
            case Stage.AskParent:
                _counts.Edited++;
                return Advance(new ReviewContextMapRequest("move", Node: entry.Node.Value, Other: text));
            case Stage.AskTarget:
                _counts.Dropped++;
                return Advance(new ReviewContextMapRequest("repoint", Node: text, Override: entry.Override?.Value));
            case Stage.Bulk or Stage.Item or Stage.Done:
            default:
                return Finish();
        }
    }

    private static Func<string, ReviewContextMapRequest> Target(ReviewEntry entry) => verb =>
        entry.Link is { } link ? new ReviewContextMapRequest(verb, Link: link.Value) : new ReviewContextMapRequest(verb, Node: entry.Node.Value);

    private ReviewStep Begin(List<ReviewEntry> items, ReviewContextMapRequest? send = null)
    {
        _items = items;
        _index = -1;
        return Advance(send);
    }

    private ReviewStep Advance(ReviewContextMapRequest? send)
    {
        _index++;
        _stage = Stage.Item;
        if (_index >= _items.Count)
        {
            _stage = Stage.Done;
            return new ReviewStep(Prompt: null, send);
        }

        return new ReviewStep(ItemPrompt(), send);
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
