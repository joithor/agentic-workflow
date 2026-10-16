using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-WIRE-003 (DT-C, DT spec §8.4): a transition from a child wait produces one of the child's outputs only on the
/// outcomes whose every path inside the child produces it, and never on 'cancelled', which carries nothing. An output the
/// child doesn't declare is RCP-WIRE-002's, and a child that doesn't resolve is RCP-CHILD-001's.
/// </summary>
internal sealed partial class WiringRules
{
    private static IEnumerable<RecipeFinding> ChildOutputsProduced(Recipe recipe, ValidationContext context, RecipeSource source) =>
        recipe.Transitions
            .Select((t, i) => (Transition: t, Number: i))
            .Where(n => n.Transition.On == "wait.satisfied")
            .SelectMany(n => n.Transition.Produces.Select((entry, j) => (n.Transition, n.Number, Entry: entry, Index: j)))
            .SelectMany(p => ChildrenOf(recipe, p.Transition, context).Select(c => new ChildProduces(p.Transition, p.Number, p.Entry, p.Index, c.From, c.Child, c.Id)))
            .Where(p => !HasEmptyList(p.Transition.When) && ChildOutputs.Declared(p.Child).Contains(p.Entry.SourceKey()))   // why: an empty all/any is RCP-SCHEMA-001's, one fault gives one finding
            .Select(p => ChildOutputFinding(p, source))
            .OfType<RecipeFinding>()
            .DistinctBy(f => (f.Path, f.Rule));

    /// <summary>One 'produces' entry of a transition from a wait on a resolved child.</summary>
    private sealed record ChildProduces(RecipeTransition Transition, int Number, ProducedFact Entry, int Index, string From, Recipe Child, string Id);

    /// <summary>The resolved children the states a transition leaves wait for.</summary>
    private static IEnumerable<(string From, Recipe Child, string Id)> ChildrenOf(Recipe recipe, RecipeTransition transition, ValidationContext context) =>
        transition.From.Distinct(StringComparer.Ordinal)
            .Select(from => (From: from, Wait: recipe.States.GetValueOrDefault(from)?.WaitFor?.Child))
            .Where(w => w.Wait is not null && context.Children.Resolve(w.Wait.Recipe) is not null)
            .Select(w => (w.From, context.Children.Resolve(w.Wait!.Recipe)!.Recipe, w.Wait.Recipe.Value));   // why: the Where above keeps only resolved children

    private static RecipeFinding? ChildOutputFinding(ChildProduces at, RecipeSource source)
    {
        var (transition, number, entry, index, from, child, id) = at;
        var path = $"transitions[{Inv(number)}].produces[{Inv(index)}]";
        var fact = entry.SourceKey();
        var produces = entry.From is null ? $"produces '{entry.Fact.Value}'" : $"produces '{entry.Fact.Value}' from '{fact.Value}'";
        var present = ChildOutputs.PresentOn(child);
        var carrying = present.Where(p => p.Key != _cancelled && p.Value.Contains(fact)).Select(p => p.Key).ToList();
        var carries = carrying.Count == 0 ? $"remove it, since no outcome of {id} always carries it" : null;
        var outcomes = ChildOutcomes(transition.When, [.. present.Keys]);
        if (!NamesOutcome(transition.When))
        {
            var join = entry.From is null ? $"{produces} from child '{id}'" : $"{produces} (child '{id}')";
            return source.Finding(
                "RCP-WIRE-003", path, $"transition from '{from}' {join} on a when that names no outcome (it matches every outcome, 'cancelled' among them)",
                carries ?? $"add when: {{ outcome: <o> }} naming outcomes that always carry it ({List(carrying)})");
        }

        if (outcomes.Contains(_cancelled))
        {
            return source.Finding(
                "RCP-WIRE-003", path, $"transition from '{from}' {produces} on outcome 'cancelled' of child '{id}', which carries no outputs",
                "route cancelled with a transition of its own that produces nothing");
        }

        return MissingFinding(at, present, outcomes, carrying, source);
    }

    private static RecipeFinding? MissingFinding(ChildProduces at, IReadOnlyDictionary<OutcomeName, IReadOnlySet<FactKey>> present, HashSet<OutcomeName> outcomes, List<OutcomeName> carrying, RecipeSource source)
    {
        var (_, number, entry, index, from, child, id) = at;
        var fact = entry.SourceKey();
        var missing = present.Where(p => p.Key != _cancelled && outcomes.Contains(p.Key) && !p.Value.Contains(fact)).Select(p => (OutcomeName?)p.Key).FirstOrDefault();
        if (missing is not { } gap)
        {
            return null;
        }

        var produces = entry.From is null ? $"produces '{entry.Fact.Value}'" : $"produces '{entry.Fact.Value}' from '{fact.Value}'";
        return source.Finding(
            "RCP-WIRE-003", $"transitions[{Inv(number)}].produces[{Inv(index)}]",
            $"transition from '{from}' {produces} on outcome '{gap.Value}' of child '{id}', which doesn't produce '{fact.Value}' on every path to '{gap.Value}' (in {id}, {MissingOn(child, gap, fact)})",
            carrying.Count == 0
                ? $"produce it on that path in {id}, or remove it, since no outcome of {id} always carries it"
                : $"produce it on that path in {id}, or produce it here only on outcomes that always carry it ({List(carrying)})");
    }

    /// <summary>
    /// The outcomes of the child a 'when' may hold for, out of <paramref name="universe"/> (its terminal outcomes and 'cancelled').
    /// </summary>
    private static HashSet<OutcomeName> ChildOutcomes(WhenClause? when, HashSet<OutcomeName> universe) =>
        when is null ? [.. universe] : OutcomeBounds(when, universe).May;

    /// <summary>
    /// The outcomes a clause may hold for, and the ones it must hold for whatever the facts are. An outcome test is that
    /// outcome in both; a fact or loop test may hold anywhere and must hold nowhere; 'not' is the complement across the two
    /// (may = universe minus must, must = universe minus may); 'all' intersects both, 'any' unites both.
    /// </summary>
    private static (HashSet<OutcomeName> May, HashSet<OutcomeName> Must) OutcomeBounds(WhenClause when, HashSet<OutcomeName> universe)
    {
        var bounds = new List<(HashSet<OutcomeName> May, HashSet<OutcomeName> Must)>();
        if (when.Outcome is { } outcome)
        {
            bounds.Add(([.. universe.Where(o => o == outcome)], [.. universe.Where(o => o == outcome)]));
        }

        if (when.Fact is not null || when.Loop is not null)
        {
            bounds.Add(([.. universe], []));
        }

        if (when.Not is { } not)
        {
            var (innerMay, innerMust) = OutcomeBounds(not, universe);
            bounds.Add(([.. universe.Except(innerMust)], [.. universe.Except(innerMay)]));
        }

        if (when.All is { Count: > 0 } all)
        {
            var parts = all.Select(c => OutcomeBounds(c, universe)).ToList();
            bounds.Add((Intersect(parts.Select(p => p.May), universe), Intersect(parts.Select(p => p.Must), universe)));
        }

        if (when.Any is { Count: > 0 } any)
        {
            var parts = any.Select(c => OutcomeBounds(c, universe)).ToList();
            bounds.Add(([.. parts.SelectMany(p => p.May)], [.. parts.SelectMany(p => p.Must)]));
        }

        // why: the keys of one clause all have to hold, so the keys present combine as an 'all'
        return (Intersect(bounds.Select(b => b.May), universe), Intersect(bounds.Select(b => b.Must), universe));
    }

    private static HashSet<OutcomeName> Intersect(IEnumerable<HashSet<OutcomeName>> sets, HashSet<OutcomeName> universe)
    {
        HashSet<OutcomeName> result = [.. universe];
        sets.ToList().ForEach(result.IntersectWith);
        return result;
    }

    private static bool HasEmptyList(WhenClause? when) =>
        when is not null && (when.All is { Count: 0 } || when.Any is { Count: 0 } || (when.All ?? []).Any(HasEmptyList) || (when.Any ?? []).Any(HasEmptyList) || HasEmptyList(when.Not));

    private static bool NamesOutcome(WhenClause? when) =>
        when is not null && (when.Outcome is not null || (when.All ?? []).Any(NamesOutcome) || (when.Any ?? []).Any(NamesOutcome) || NamesOutcome(when.Not));

    /// <summary>The first path inside <paramref name="child"/> into a state that ends with <paramref name="outcome"/> that doesn't produce <paramref name="fact"/>.</summary>
    private static string MissingOn(Recipe child, OutcomeName outcome, FactKey fact)
    {
        var reaching = FactsReaching.Of(child);
        var state = child.States
            .Where(s => s.Value.Kind == StateKind.Terminal && s.Value.Outcome == outcome && reaching.TryGetValue(s.Key, out var facts) && !facts.Contains(fact))
            .Select(s => s.Key)
            .FirstOrDefault();
        return state is null
            ? "no state of it ends with that outcome on a path from its start"
            : FactsReaching.Witness(child, reaching, state, fact);
    }
}
