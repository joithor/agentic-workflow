using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Builds the list row (<see cref="InstanceSummary"/>) from an <see cref="InstanceView"/> (UI screens E-20).</summary>
public static class InstanceSummaries
{
    /// <summary>The longest 'now' text, ellipsis included.</summary>
    public const int NowLength = 80;

    /// <summary>The summary of <paramref name="view"/>.</summary>
    /// <param name="view">The instance view.</param>
    /// <returns>The row: the view's identity, recipe, state, layout cells, and its 'next' cut to <see cref="NowLength"/>.</returns>
    public static InstanceSummary Of(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new InstanceSummary(
            view.Address.Value,
            view.ItemId,
            new RecipeRef(view.Recipe.Id, view.Recipe.Hash.Value, view.Recipe.Version, view.Urgency),
            view.State,
            view.Since,
            view.EngineOwned,
            view.Subject,
            view.Inputs,
            view.States,
            Cut(view.Next),
            [.. view.AllowedCommands.Select(static c => c.Action)]);
    }

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
