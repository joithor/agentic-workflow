using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Builds the list row (<see cref="InstanceSummary"/>) from an <see cref="InstanceView"/> (UI screens E-20).</summary>
public static class InstanceSummaries
{
    /// <summary>The longest 'now' text, ellipsis included.</summary>
    public const int NowLength = 80;

    /// <summary>The list row of <paramref name="view"/>: a pure function of the view and the context (no clock, no I/O).</summary>
    /// <param name="view">The instance view.</param>
    /// <param name="context">What the row needs besides the view; null reads as a started instance.</param>
    /// <returns>The row: the view's identity, recipe, chip, layout cells, its answer line and its 'next' cut to <see cref="NowLength"/>.</returns>
    public static InstanceSummary Of(InstanceView view, InstanceSummaryContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var status = view.Status ?? throw new ArgumentException("the view has no status record; build it with InstanceViews.Of", nameof(view));
        return new InstanceSummary(
            view.Address,
            view.Id,
            view.ItemId,
            new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, view.Recipe.Version, view.Urgency),
            view.State,
            StateChip.For(view.EngineOwned, deskOpen: view.Task is not null, terminal: view.Outcome is not null, context?.NotStarted ?? false),
            view.Since,
            view.EngineOwned,
            view.Subject ?? NoSubject(view),
            view.Inputs,
            view.States,
            Cut(view.Next),
            status.Answer,
            [.. status.Commands.Select(static c => c.ActionId)]);
    }

    /// <summary>The subject of an instance whose recipe declares no input: the item itself.</summary>
    internal static SubjectRef NoSubject(InstanceView view) => new("item", view.ItemId?.Value ?? string.Empty, view.Address.Value, Href: null);

    /// <summary>The text cut to <see cref="NowLength"/> with the ellipsis inside it, never between the two halves of a surrogate pair.</summary>
    private static string Cut(string next)
    {
        if (next.Length <= NowLength)
        {
            return next;
        }

        var keep = NowLength - 3;
        return string.Concat(next.AsSpan(0, char.IsHighSurrogate(next[keep - 1]) ? keep - 1 : keep), "...");
    }
}
