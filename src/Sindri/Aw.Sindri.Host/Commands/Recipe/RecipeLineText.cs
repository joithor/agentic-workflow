using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>A recipe's line as 'recipe list', 'recipe show' and 'recipe register' print it (engine spec §6.3). Pure.</summary>
internal static class RecipeLineText
{
    /// <summary>
    /// '&lt;id&gt;@&lt;hash12&gt; &lt;standing&gt;', with '(desk)' or '(freeze)' when approved or revoked, and the stale reasons
    /// with the fix, for example 'workflow.step@9ab2d40c1e77 approved (desk)'.
    /// </summary>
    /// <param name="s">The recipe's line.</param>
    /// <returns>The line, without a newline.</returns>
    internal static string Line(RecipeSummary s)
    {
        var name = $"{s.Id.Value}@{s.Hash.Display}";
        var via = s.Via is { } v && s.Standing != RecipeStanding.Unapproved ? $" ({Word(v)})" : string.Empty;
        var stale = s.Stale.Count == 0
            ? string.Empty
            : $"; stale: {string.Join(", ", s.Stale.Select(Word))} (fix: sindri recipe approve {name})";
        return $"{name} {Word(s.Standing)}{via}{stale}";
    }

    /// <summary>One line per recipe, or the empty state: 'no recipes registered', or 'no stale recipes' for '--stale'.</summary>
    /// <param name="lines">The recipes.</param>
    /// <param name="stale">Whether only the stale ones were asked for.</param>
    /// <returns>The text.</returns>
    internal static string List(IReadOnlyList<RecipeSummary> lines, bool stale) => lines.Count == 0
        ? (stale ? "no stale recipes\n" : "no recipes registered\n")
        : string.Concat(lines.Select(static l => Line(l) + "\n"));

    /// <summary>'unapproved', 'approved' or 'revoked'.</summary>
    /// <param name="standing">Where the recipe stands.</param>
    /// <returns>The word.</returns>
    internal static string Word(RecipeStanding standing) => standing switch
    {
        RecipeStanding.Unapproved => "unapproved",
        RecipeStanding.Approved => "approved",
        RecipeStanding.Revoked => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(standing), standing, "Not a recipe standing."),
    };

    private static string Word(ApprovalVia via) => via == ApprovalVia.Freeze ? "freeze" : "desk";

    private static string Word(StaleReason reason) => reason == StaleReason.ChildStale ? "child-stale" : "pin-drift";
}
