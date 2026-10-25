using Aw.Sindri.Host.Registration;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>The default profile's allowed effects come from the embedded recipes, read through the one embedding (setup-once spec §5.1).</summary>
public sealed class EmbeddedShippedEffectsTests
{
    private static string Recipe(string name, params string[] effects) =>
        "schema: 1\n"
        + $"id: workflow.{name}\n"
        + "version: 1\n"
        + "inputs: [{ key: fact:spec, label: \"Spec\", type: text, kind: text }]\n"
        + "initial: produce\n"
        + "states:\n"
        + "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the output.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + $"  - {{ from: produce, to: done, on: step.completed, when: {{ outcome: done }}, produces: [fact:output], effects: [{string.Join(", ", effects)}] }}\n";

    private static EmbeddedShippedEffects Subject(FakeEmbeddedRecipes embedded) => new(embedded, RecipeReaderTests.Reader);

    [Fact, Trait("Lane", "unit")]
    public void No_embedded_recipes_ship_no_effect()
        => Assert.Empty(Subject(FakeEmbeddedRecipes.Empty).Kinds());

    [Fact, Trait("Lane", "unit")]
    public void The_kinds_are_the_union_of_the_effects_the_embedded_recipes_declare_sorted_and_once()
    {
        var subject = Subject(FakeEmbeddedRecipes.Of(
            ("workflow.b.yaml", Recipe("b", "effect.notify.desk", "effect.claim.ref")),
            ("workflow.a.yaml", Recipe("a", "effect.notify.desk"))));

        Assert.Equal(["effect.claim.ref", "effect.notify.desk"], subject.Kinds(), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Eval_cases_and_a_file_that_does_not_bind_add_no_effect()
    {
        var subject = Subject(FakeEmbeddedRecipes.Of(
            ("workflow.a.yaml", Recipe("a", "effect.claim.ref")),
            ("workflow.broken.yaml", "not: [a recipe"),
            ("evals/workflow.a/case.jsonl", "{}")));

        Assert.Equal(["effect.claim.ref"], subject.Kinds(), StringComparer.Ordinal);
    }
}
