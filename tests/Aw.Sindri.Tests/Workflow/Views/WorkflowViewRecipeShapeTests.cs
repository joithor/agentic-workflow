using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>
/// A recipe is declared once: every instance of one (recipe, hash) carries the same states, loops, desk flags and fact rows, a
/// fixture recipe equals its file under recipes/ where one exists, and a fixture-only recipe obeys the validator's declaration
/// rules (RCP-TYPE-002/003, a terminal state, RCP-OUT-002), so the goldens are what the engine will serve.
/// </summary>
public sealed class WorkflowViewRecipeShapeTests
{
    private sealed record Instance(string Address, InstanceRecipe Recipe, IReadOnlyList<StateCell> States, IReadOnlyList<InputPresence> Inputs, IReadOnlyList<FactRow> Facts);

    private static List<Instance> Instances() =>
    [
        .. WorkflowViewFixtures.RootList().Instances.Select(r => new Instance(r.Address.Value, r.Recipe, r.States, r.Inputs, [])),
        .. WorkflowViewFixtures.AllDetails().Select(d => new Instance(d.Address.Value, d.Recipe, d.States, [], d.Facts)),
    ];

    private static IEnumerable<IGrouping<string, Instance>> ByRecipeHash() => Instances().GroupBy(i => $"{i.Recipe.Id.Value}@{i.Recipe.Hash.Value}", StringComparer.Ordinal);

    private static string Cell(StateCell c) =>
        string.Create(CultureInfo.InvariantCulture, $"{c.State}|{c.Order}|{c.BranchOf}|desk={c.Desk}|child={c.Child}|terminal={c.Terminal}|loops={string.Join(',', c.Loops.Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.With}/{l.Max}")).Order(StringComparer.Ordinal))}");

    private static string Declared(FactRow f) => $"{f.Key}|{f.Label}|{f.Type}|{f.Direction}|{f.From}";

    [Fact, Trait("Lane", "unit")]
    public void Every_instance_of_a_recipe_hash_declares_the_same_states_loops_desk_flags_and_fact_rows()
    {
        foreach (var group in ByRecipeHash())
        {
            var states = group.Select(i => string.Join(';', i.States.Select(Cell))).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(states.Count == 1, $"{group.Key}: {states.Count} different state lists");
            var facts = group.Where(i => i.Facts.Count > 0).Select(i => string.Join(';', i.Facts.Select(Declared).Order(StringComparer.Ordinal))).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(facts.Count <= 1, $"{group.Key}: {facts.Count} different fact-row lists");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_recipe_equals_its_file_under_recipes_where_one_exists()
    {
        var files = ShippedRecipes.Recipes
            .Select(p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), p))
            .ToDictionary(d => d.Recipe.Id.Value, StringComparer.Ordinal);
        var compared = 0;
        foreach (var group in ByRecipeHash().Where(g => files.ContainsKey(g.First().Recipe.Id.Value)))
        {
            var file = files[group.First().Recipe.Id.Value];
            var recipe = group.First().Recipe;
            Assert.Equal(file.Hash.Value, recipe.Hash.Value);
            Assert.Equal(file.Recipe.Version, recipe.Version);
            var order = StatePathOrder.Of(file.Recipe);
            Assert.Equal(order.States.Select(s => (s.State, s.Order, s.BranchOf)), group.First().States.Select(c => (c.State, c.Order, c.BranchOf)));
            Assert.Equal(file.Recipe.States.Where(s => s.Value.Kind == StateKind.Terminal).Select(s => s.Key).Order(StringComparer.Ordinal),
                group.First().States.Where(c => c.Terminal).Select(c => c.State).Order(StringComparer.Ordinal));
            var facts = group.SelectMany(i => i.Facts).DistinctBy(Declared, StringComparer.Ordinal).ToList();
            if (facts.Count > 0)
            {
                Assert.Equal(file.Recipe.Outputs.Select(o => (o.Key.Value, o.From)).Order(),
                    facts.Where(f => f.Direction == FactDirection.Output).Select(f => (f.Key, f.From!)).Order());
                Assert.Equal(file.Recipe.Inputs.Select(i => i.Key.Value).Order(StringComparer.Ordinal), facts.Where(f => f.Direction == FactDirection.Input).Select(f => f.Key).Order(StringComparer.Ordinal));
            }

            compared++;
        }

        Assert.True(compared >= 3, "the dev recipes and workflow.step are compared");
    }

    // The shipped catalog says which Steps are deskCapable; the engine runs those at the desk until the agent runtime (WF 12, W20).
    private static readonly BlockCatalog _catalog = FixtureCatalog.Shipped();

    private static string M(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_recipe_declaration_equals_its_file_in_every_field_the_file_carries()
    {
        var files = ShippedRecipes.Recipes
            .Select(p => RecipeComposition.ReadClean(ShippedRecipes.Read(p), p))
            .ToDictionary(d => d.Recipe.Id.Value, StringComparer.Ordinal);
        var compared = FixtureRecipes.All.Where(s => files.ContainsKey(s.Id)).ToList();
        var failures = compared.SelectMany(shape => Mismatches(shape, files[shape.Id])).ToList();
        Assert.True(failures.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, failures));
        Assert.Contains(compared, s => s.Id == "workflow.step");
        Assert.Contains(compared, s => s.Id == "workflow.dev-desk-round-trip");
        Assert.Contains(compared, s => s.Id == "workflow.dev-triage-three-state");
    }

    private static List<string> Mismatches(RecipeShape shape, RecipeDocument file)
    {
        var recipe = file.Recipe;
        var failures = new List<string>();
        if (file.Hash.Value != shape.Stamp.Hash.Value || recipe.Version != shape.Version || Urgencies.Parse(recipe.Urgency) != shape.Urgency || recipe.Budget?.Tokens != shape.BudgetTokens)
        {
            failures.Add(M($"{shape.Id}: hash, version, urgency or recipe budget differ from the file"));
        }

        if (!recipe.States.Keys.Order(StringComparer.Ordinal).SequenceEqual(shape.States.Select(s => s.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the state ids differ from the file"));
        }

        failures.AddRange(shape.States.Where(s => recipe.States.ContainsKey(s.Id)).SelectMany(s => StateMismatches(shape.Id, s, recipe.States[s.Id])));
        failures.AddRange(LoopMismatches(shape, recipe));
        failures.AddRange(FactMismatches(shape, recipe));
        return failures;
    }

    private static List<string> StateMismatches(string id, ShapeState state, RecipeState declared)
    {
        var file = FileSignature(declared);
        var shape = M($"{state.Kind}|{state.ChildRecipe}|{state.OnParked}|{state.Desk}|{state.Block}|{state.Timeout?.After}|{state.Timeout?.Then}|{state.BudgetTokens}");
        return file == shape ? [] : [M($"{id}/{state.Id}: kind|child|onParked|desk|block|timeout|then|budget is {shape}, the file has {file} (an active deskCapable Step runs at the desk until P8: WF 12, W20)")];
    }

    private static string FileSignature(RecipeState declared)
    {
        var deskCapable = declared.Kind == StateKind.Active && declared.Step is { } step && _catalog.Find(step.Use) is { DeskCapable: true };
        var child = declared.WaitFor?.Child;
        return M($"{declared.Kind}|{child?.Recipe.Value}|{child?.OnParked}|{deskCapable}|{declared.Step?.Use.Value}|{declared.Timeout?.After.Span}|{declared.Timeout?.Then}|{declared.Budget?.Tokens}");
    }

    private static List<string> LoopMismatches(RecipeShape shape, Recipe recipe)
    {
        static string Edge(string? name, string from, string to) => M($"{name}:{from}>{to}");
        var failures = new List<string>();
        var file = StatePathOrder.Of(recipe).Loops.Select(l => Edge(l.Name, l.From, l.To)).Order(StringComparer.Ordinal);
        if (!file.SequenceEqual(shape.Loops.Select(l => Edge(l.Name, l.From, l.To)).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the loop edges differ from the file"));
        }

        failures.AddRange(shape.Loops.Where(l => !recipe.Loops.TryGetValue(l.Name, out var spec) || spec.Cap != l.Max).Select(l => M($"{shape.Id}: loop {l.Name} has another cap in the file")));
        return failures;
    }

    private static List<string> FactMismatches(RecipeShape shape, Recipe recipe)
    {
        var failures = new List<string>();
        var inputs = recipe.Inputs.Select(i => M($"{i.Key.Value}|{i.Label}|{i.Type ?? FactTypes.Default}|{i.Kind}"));
        if (!inputs.SequenceEqual(shape.Inputs.Select(i => M($"{i.Key}|{i.Label}|{FactTypeWord(i.Type)}|{i.Kind}")), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the inputs (key, label, type, kind) differ from the file"));
        }

        var outputs = recipe.Outputs.Select(o => M($"{o.Key.Value}|{o.Label}|{o.Type ?? FactTypes.Default}|{o.From}"));
        if (!outputs.SequenceEqual(shape.Outputs.Select(o => M($"{o.Key}|{o.Label}|{FactTypeWord(o.Type)}|{o.From}")), StringComparer.Ordinal))
        {
            failures.Add(M($"{shape.Id}: the outputs (key, label, type, from) differ from the file"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fixture_only_recipe_passes_the_validator_declaration_rules()
    {
        foreach (var group in ByRecipeHash())
        {
            var states = group.First().States;
            Assert.Contains(states, c => c.Terminal);
            foreach (var fact in group.SelectMany(i => i.Facts).Where(f => f.Direction == FactDirection.Output))
            {
                Assert.False(states.Single(c => c.State == fact.From).Terminal, $"{group.Key}: {fact.Key} comes from a terminal state (RCP-OUT-002)");
            }

            foreach (var input in group.SelectMany(i => i.Inputs).Where(i => i.Kind is not null))
            {
                Assert.True(SubjectKinds.IsKnown(input.Kind!), $"{group.Key}: kind {input.Kind} (RCP-TYPE-002)");
                var fact = group.SelectMany(i => i.Facts).FirstOrDefault(f => f.Key == input.Key);
                if (fact is not null && SubjectKinds.PairedType(input.Kind!) is { } paired)
                {
                    Assert.Equal(paired, FactTypeWord(fact.Type));
                }
            }
        }
    }

    private static string FactTypeWord(FactType type) => type switch
    {
        FactType.ScopeMap => "scope-map",
        FactType.Text or FactType.File or FactType.Item or FactType.Link or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload => type.ToString().ToLowerInvariant(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no such fact type"),
    };

    [Fact, Trait("Lane", "unit")]
    public void A_child_has_a_real_state_of_its_recipe_and_a_not_started_one_offers_open_item_only()
    {
        var stateIds = Instances().GroupBy(i => i.Recipe.Id.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.SelectMany(i => i.States.Select(c => c.State)).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            foreach (var group in view.Children)
            {
                foreach (var row in group.Rows.Concat(group.Workstreams.SelectMany(w => w.Rows)))
                {
                    Assert.Contains(row.State, stateIds[group.Recipe.Value]);
                    Assert.NotEqual("queued", row.State, StringComparer.Ordinal);
                    if (row.Glyph != StateGlyph.Active)
                    {
                        Assert.Equal(["OpenItem"], row.Actions.Select(a => a.Value), StringComparer.Ordinal);
                    }

                    Assert.Equal(row.Glyph == StateGlyph.Pending ? "QUEUED" : row.Chip.Word, row.Chip.Word);
                }
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parked_child_shows_its_park_on_the_parents_band_without_parking_the_parent()
    {
        var project = WorkflowViewFixtures.ProjectBuild();
        var row = project.Children.Single().Workstreams.SelectMany(w => w.Rows).Single(r => r.ItemId!.Value.Value == "ITM-01JD7");
        Assert.Equal(EngineOwnedKind.Parked, row.MirroredPark?.Kind);
        Assert.Null(project.EngineOwned);
        Assert.Equal("build", project.Current.Status.Header.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_item_instance_at_produce_round_two_of_three_has_its_parent()
    {
        var view = WorkflowViewFixtures.ItemProduce();
        Assert.Equal("ITM-01JE7/item/1/step.1", view.Address.Value);
        Assert.Equal("ITM-01JE7", view.ItemId!.Value.Value);
        Assert.Equal("ITM-01JE7/item/1", view.Parent!.Address.Value);
        Assert.Equal("produce", view.State);
        Assert.Equal((2, 3), (view.States.Single(c => c.State == "produce").Round!.N, view.States.Single(c => c.State == "produce").Round!.Max));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_goldens_cover_a_missing_required_input_and_a_child_group_with_more_rows()
    {
        Assert.Contains(WorkflowViewFixtures.RootList().Instances, r => r.Inputs.Any(i => i.Required && !i.Present));
        Assert.Contains(WorkflowViewFixtures.AllDetails().SelectMany(d => d.Children), g => g.More > 0);
    }
}
