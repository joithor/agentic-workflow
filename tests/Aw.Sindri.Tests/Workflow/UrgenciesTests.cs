using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The one parser of a recipe's urgency word (spec 3.2; RCP-URG-001 validates it, so a validated recipe never reaches Parse with a bad one).</summary>
public sealed class UrgenciesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(null, RecipeUrgency.Work)]
    [InlineData("work", RecipeUrgency.Work)]
    [InlineData("incident", RecipeUrgency.Incident)]
    [InlineData("triage", RecipeUrgency.Triage)]
    public void Parse_maps_the_urgency_word_and_an_absent_one_is_work(string? word, RecipeUrgency expected) =>
        Assert.Equal(expected, Urgencies.Parse(word));

    [Theory, Trait("Lane", "unit")]
    [InlineData("urgent")]
    [InlineData("Incident")]
    [InlineData("")]
    public void Parse_refuses_any_other_word(string word) =>
        _ = Assert.Throws<ArgumentException>(() => Urgencies.Parse(word));

    [Fact, Trait("Lane", "unit")]
    public void Every_listed_urgency_parses_to_its_own_enum_value() =>
        Assert.Equal([RecipeUrgency.Incident, RecipeUrgency.Triage, RecipeUrgency.Work], Urgencies.All.Select(Urgencies.Parse));
}
