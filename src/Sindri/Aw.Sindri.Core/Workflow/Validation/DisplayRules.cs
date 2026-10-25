using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The plain-words rules (setup-once spec §5.3 "displayName and versions"): RCP-DISPLAY-001 (a recipe's <c language="text">displayName</c> is required: sentence case, at most
/// 60 characters, what the recipe does, not an id), RCP-DISPLAY-002 (the optional <c language="text">why</c> of a recipe or a state, and a desk state's <c language="text">onOutcome</c> texts, each keyed by an outcome of the state's Step: plain words, at most 200 characters) and RCP-DISPLAY-003 (a state's
/// optional <c language="text">displayName</c> and a transition's optional <c language="text">label</c>: plain words, at most 60 characters). The app shows these instead of ids, so none holds an id or code name.
/// </summary>
internal sealed class DisplayRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var recipe = document.Recipe;
        var source = document.Source;
        return
        [
            .. RecipeName(recipe, source),
            .. RecipeWhy(recipe, source),
            .. recipe.States.SelectMany(s => State(s.Key, s.Value, source)),
            .. recipe.States.SelectMany(s => OnOutcome(recipe, s.Key, s.Value, context, source)),
            .. recipe.Transitions.SelectMany((t, i) => Label(t, string.Create(CultureInfo.InvariantCulture, $"transitions[{i}]"), source)),
        ];
    }

    private static IEnumerable<RecipeFinding> RecipeName(Recipe recipe, RecipeSource source)
    {
        if (recipe.DisplayName is null)
        {
            yield return source.Finding("RCP-DISPLAY-001", "displayName", "the recipe has no displayName",
                "add displayName: a name in sentence case, at most 60 characters, that says what the recipe does ('Idea to merged pull request')");
        }
        else if (PlainWords.Problem(recipe.DisplayName, PlainWords.MaxName, sentenceCase: true) is { } problem)
        {
            yield return source.Finding("RCP-DISPLAY-001", "displayName", $"displayName '{recipe.DisplayName}': {problem}",
                "write a name in sentence case, at most 60 characters, that says what the recipe does, with no id");
        }
    }

    private static IEnumerable<RecipeFinding> RecipeWhy(Recipe recipe, RecipeSource source)
    {
        if (recipe.Why is { } why && PlainWords.Problem(why, PlainWords.MaxWhy, sentenceCase: false) is { } problem)
        {
            yield return source.Finding("RCP-DISPLAY-002", "why", $"the why of the recipe: {problem}",
                "write one plain sentence of at most 200 characters, with no id or code name");
        }
    }

    private static IEnumerable<RecipeFinding> OnOutcome(Recipe recipe, string name, RecipeState state, ValidationContext context, RecipeSource source)
    {
        if (state.OnOutcome is not { } texts)
        {
            yield break;
        }

        var outcomes = state.Step is { } step && context.Catalog.Find(step.Use) is { } descriptor ? StepOutcomes.Of(descriptor, step, recipe) ?? descriptor.Outcomes : null;
        foreach (var (outcome, text) in texts)
        {
            var path = $"states.{name}.onOutcome.{outcome}";
            if (state.Step is null)
            {
                yield return source.Finding("RCP-DISPLAY-002", path, $"state '{name}' has onOutcome but runs no Step",
                    "remove onOutcome, or put it on a state that runs a Step");
            }
            else if (outcomes is not null && !outcomes.Any(o => string.Equals(o.Value, outcome, StringComparison.Ordinal)))
            {
                yield return source.Finding("RCP-DISPLAY-002", path, $"the onOutcome of state '{name}' names '{outcome}', which its Step doesn't report ({string.Join(", ", outcomes.Select(static o => o.Value))})",
                    "key each onOutcome text by an outcome of the state's Step");
            }

            if (PlainWords.Problem(text, PlainWords.MaxWhy, sentenceCase: false) is { } problem)
            {
                yield return source.Finding("RCP-DISPLAY-002", path, $"the onOutcome text for '{outcome}' of state '{name}': {problem}",
                    "write one plain sentence of at most 200 characters, with no id or code name");
            }
        }
    }

    private static IEnumerable<RecipeFinding> State(string name, RecipeState state, RecipeSource source)
    {
        if (state.Why is { } why && PlainWords.Problem(why, PlainWords.MaxWhy, sentenceCase: false) is { } whyProblem)
        {
            yield return source.Finding("RCP-DISPLAY-002", $"states.{name}.why", $"the why of state '{name}': {whyProblem}",
                "write one plain sentence of at most 200 characters, with no id or code name");
        }

        if (state.DisplayName is { } display && PlainWords.Problem(display, PlainWords.MaxName, sentenceCase: true) is { } displayProblem)
        {
            yield return source.Finding("RCP-DISPLAY-003", $"states.{name}.displayName", $"the displayName of state '{name}': {displayProblem}",
                "write a name in sentence case, at most 60 characters, with no id");
        }
    }

    private static IEnumerable<RecipeFinding> Label(RecipeTransition transition, string path, RecipeSource source)
    {
        if (transition.Label is { } label && PlainWords.Problem(label, PlainWords.MaxName, sentenceCase: false) is { } problem)
        {
            yield return source.Finding("RCP-DISPLAY-003", path + ".label", $"the label of the transition to '{transition.To}': {problem}",
                "write plain words of at most 60 characters ('gaps found: draft again'), with no id or code name");
        }
    }
}
