using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>A recipe's line as 'recipe list' and 'recipe register' print it (engine spec §6.3). Pure.</summary>
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
            : $"; stale: {string.Join(", ", s.Stale.Select(Word))} (fix: {(s.Stale.Contains(StaleReason.LegacyShape) ? SubjectKinds.LegacyFix : $"sindri recipe approve {name}")})";
        return $"{name} {Word(s.Standing)}{via}{stale}";
    }

    /// <summary>
    /// 'recipe show' as the served <see cref="RecipeView"/> reads: '&lt;id&gt;@&lt;hash12&gt;', 'urgency: &lt;urgency&gt;', 'name: &lt;template&gt;' when the recipe has one, one 'input: &lt;key&gt; type=&lt;type&gt; kind=&lt;kind&gt;' line per input
    /// (a generate template is added as generate="...") and one 'output: &lt;key&gt; type=&lt;type&gt; from=&lt;state&gt;' line per output, then the path order (spec §3.10): 'order: &lt;path|transition-list&gt;', one 'state: &lt;name&gt; order=&lt;n&gt;' (or 'branch-of=&lt;state&gt;', or 'order=-' for a state the walk never reaches) line per state and one 'loop: &lt;name&gt; &lt;from&gt; -&gt; &lt;to&gt; cap=&lt;n&gt;' line per loop.
    /// </summary>
    /// <param name="v">The served recipe view.</param>
    /// <returns>The lines, each with its newline.</returns>
    internal static string Show(RecipeView v) =>
        $"{v.Id.Value}@{v.Hash.Display}\n"
        + $"urgency: {v.Urgency.ToString().ToLowerInvariant()}\n"
        + (v.Name is { } name ? $"name: {name}\n" : string.Empty)
        + string.Concat(v.Inputs.Select(static i => $"input: {i.Key} type={FactTypeJsonConverter.Word(i.Type)} kind={i.Kind}{(i.Generate is { } generate ? $" generate=\"{generate.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : string.Empty)}\n"))
        + string.Concat(v.Outputs.Select(static o => $"output: {o.Key} type={FactTypeJsonConverter.Word(o.Type)} from={o.From}\n"))
        + $"order: {(v.OrderedBy == PathOrdering.TransitionList ? "transition-list" : "path")}\n"
        + string.Concat(v.States.Select(static p => $"state: {p.State} {(p.BranchOf is { } b ? $"branch-of={b}" : $"order={p.Order?.ToString(CultureInfo.InvariantCulture) ?? "-"}")}\n"))
        + string.Concat(v.Loops.Select(static l => string.Create(CultureInfo.InvariantCulture, $"loop: {l.Name ?? "-"} {l.From} -> {l.To} cap={l.Cap}\n")));

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

    private static string Word(StaleReason reason) => reason switch
    {
        StaleReason.ChildStale => "child-stale",
        StaleReason.LegacyShape => "legacy-shape",
        StaleReason.PinDrift or _ => "pin-drift",
    };
}
