using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The declaration rules (spec §3.2, §3.3, §9.1 "Added (UI E-21, E-22)"): RCP-URG-001, RCP-OUT-001/002/003 and
/// RCP-TYPE-001/002/003. Pure: they read the bound recipe and the catalog. They check that what a recipe declares for
/// the app (its urgency, outputs and input types) is well formed and names real things; they add no wiring. A block or
/// event that isn't found is the block rules' to report, so the output check skips a state whose source it can't resolve.
/// </summary>
internal sealed class DeclarationRules : IRecipeRules
{
    private static readonly string[] _urgencies = ["incident", "triage", "work"];

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var source = document.Source;
        return
        [
            .. Urgency(recipe, source),
            .. Inputs(recipe, source),
            .. Outputs(recipe, context, source),
        ];
    }

    private static IEnumerable<RecipeFinding> Urgency(Recipe recipe, RecipeSource source)
    {
        if (!_urgencies.Contains(recipe.Urgency, StringComparer.Ordinal))
        {
            yield return source.Finding("RCP-URG-001", "urgency",
                $"urgency '{recipe.Urgency}' isn't one of {string.Join(", ", _urgencies)}",
                "use incident, triage or work, or remove urgency (the default is work)");
        }
    }

    private static IEnumerable<RecipeFinding> Inputs(Recipe recipe, RecipeSource source) =>
        recipe.Inputs.SelectMany((input, i) => Input(input, $"inputs[{Inv(i)}]", source));

    private static IEnumerable<RecipeFinding> Input(RecipeInput input, string path, RecipeSource source)
    {
        var what = $"input '{input.Key.Value}'";
        var badType = TypeFinding(source, path, what, input.Type);
        var badKind = input.Kind is { } kind && !SubjectKinds.IsKnown(kind)
            ? source.Finding("RCP-TYPE-002", $"{path}.kind",
                $"{what} has kind '{kind}', which isn't one of {string.Join(", ", SubjectKinds.All)}", "use one of them")
            : null;
        if (badType is not null || badKind is not null)
        {
            return [.. new[] { badType, badKind }.OfType<RecipeFinding>()];
        }

        return input.Kind is { } paired && SubjectKinds.PairedType(paired) is { } expected
            && !string.Equals(input.Type ?? FactTypes.Default, expected, StringComparison.Ordinal)
            ? [source.Finding("RCP-TYPE-003", $"{path}.kind",
                $"{what} has kind '{paired}' but type {(input.Type is null ? $"'{FactTypes.Default}' (the default)" : $"'{input.Type}'")}; kind {paired} pairs with type {expected}",
                $"set type to {expected}, or change kind")]
            : [];
    }

    private static RecipeFinding? TypeFinding(RecipeSource source, string path, string what, string? type) =>
        type is not null && !FactTypes.IsKnown(type)
            ? source.Finding("RCP-TYPE-001", $"{path}.type",
                $"{what} has type '{type}', which isn't one of {string.Join(", ", FactTypes.All)}", "use one of them")
            : null;

    private static IEnumerable<RecipeFinding> Outputs(Recipe recipe, ValidationContext context, RecipeSource source)
    {
        var first = new Dictionary<FactKey, int>();
        for (var i = 0; i < recipe.Outputs.Count; i++)
        {
            var output = recipe.Outputs[i];
            var at = $"outputs[{Inv(i)}]";
            var what = $"output '{output.Key.Value}'";
            if (TypeFinding(source, at, what, output.Type) is { } badType)
            {
                yield return badType;
            }

            foreach (var finding in Key(recipe, output, i, first, source))
            {
                yield return finding;
            }

            if (From(recipe, output, at, context, source) is { } from)
            {
                yield return from;
            }
        }
    }

    private static IEnumerable<RecipeFinding> Key(Recipe recipe, RecipeOutput output, int index, Dictionary<FactKey, int> first, RecipeSource source)
    {
        var path = $"outputs[{Inv(index)}].key";
        if (first.TryGetValue(output.Key, out var earlier))
        {
            yield return source.Finding("RCP-OUT-003", path,
                $"output key '{output.Key.Value}' is declared twice (first at outputs[{Inv(earlier)}])",
                "remove the duplicate, or give it its own key");
        }
        else
        {
            first[output.Key] = index;
            if (recipe.Inputs.Any(i => i.Key == output.Key))
            {
                yield return source.Finding("RCP-OUT-003", path,
                    $"output key '{output.Key.Value}' is also an input key",
                    "use another key for the output, since an input is already a fact the recipe starts with");
            }
        }
    }

    private static RecipeFinding? From(Recipe recipe, RecipeOutput output, string at, ValidationContext context, RecipeSource source)
    {
        var path = $"{at}.from";
        var key = output.Key.Value;
        var name = output.From;
        if (SchemaRules.ReservedStates.Contains(name))
        {
            return source.Finding("RCP-OUT-002", path,
                $"output '{key}' comes from '{name}', a state the engine owns, not one of the recipe's",
                "name the recipe state that produces it");
        }

        if (!recipe.States.TryGetValue(name, out var state))
        {
            return source.Finding("RCP-OUT-001", path,
                $"output '{key}' comes from state '{name}', which the recipe doesn't declare",
                $"name one of its states: {string.Join(", ", recipe.States.Keys)}");
        }

        if (state.Kind == StateKind.Terminal)
        {
            return source.Finding("RCP-OUT-002", path,
                $"output '{key}' comes from terminal state '{name}', which produces nothing",
                "name the state that produces it");
        }

        return Unrecorded(recipe, output, path, context, source);
    }

    /// <summary>OUT-002 for a state that records nothing like the output: an error, or information when the state waits on a child whose outcome can't be read here.</summary>
    private static RecipeFinding? Unrecorded(Recipe recipe, RecipeOutput output, string path, ValidationContext context, RecipeSource source)
    {
        var state = recipe.States[output.From];
        var key = output.Key.Value;
        var name = output.From;
        // an input is a fact the recipe starts with, so it needs no state to record it
        var produced = recipe.Transitions.Where(t => t.From.Contains(name, StringComparer.Ordinal)).SelectMany(t => t.Produces);
        if (produced.Contains(output.Key) || recipe.Inputs.Any(i => i.Key == output.Key))
        {
            return null;
        }

        if (state.WaitFor?.Child is { } child)
        {
            return source.Finding("RCP-OUT-002", path,
                $"output '{key}' comes from state '{name}', which waits on child '{child.Recipe.Value}'; whether the child's outcome carries it can't be checked here",
                $"list '{key}' in 'produces' on the transition leaving '{name}' if the recipe should record it",
                FindingSeverity.Information);
        }

        if (RecordedBy(state, context) is not { } recorded || recorded.Facts.Contains(output.Key))
        {
            return null;
        }

        var records = recorded.Facts.Count == 0 ? "nothing" : string.Join(", ", recorded.Facts.Select(f => f.Value));
        return source.Finding("RCP-OUT-002", path,
            $"output '{key}' comes from state '{name}', but nothing there records it ({recorded.Name} records {records})",
            $"have the state's Step, a transition leaving it ('produces') or its wait record '{key}', or name the state that does");
    }

    /// <summary>What a state's own trigger records: a Step's outputs, an event's facts, an answer's fact; null when its block or event can't be found (the block rules report that).</summary>
    private static Recorded? RecordedBy(RecipeState state, ValidationContext context) => state switch
    {
        { Step: { } step } => context.Catalog.Find(step.Use) is { Kind: BlockKind.Step } descriptor
            ? new Recorded($"step '{step.Use.Value}'", [.. descriptor.Outputs.Select(o => o.Fact)])
            : null,
        { WaitFor.Event: { } kind } => context.Catalog.FindEvent(kind) is { } e ? new Recorded($"event '{kind.Value}'", e.Facts) : null,
        { WaitFor.Answer: { } answer } => new Recorded($"answer '{answer.Id.Value}'", [FactKey.Answer]),
        { WaitFor.Resource: not null } => new Recorded("resource", []),
        _ => null,
    };

    private sealed record Recorded(string Name, IReadOnlyList<FactKey> Facts);

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
