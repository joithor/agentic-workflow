using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>
/// The typed list filters compiled to a visibility query (engine spec §6.3, API spec §3.8): filters AND together, text is
/// quoted, and a filter on an attribute whose default an instance may never have set also matches the instances without
/// it (an instance started before the attribute existed has no 'Urgency' or 'Root', and reads as 'work' and root).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFiltersTests
{
    [Fact]
    public void No_filter_is_no_query() =>
        Assert.Null(InstanceFilters.ToQuery(new InstanceFilter()));

    [Theory]
    [MemberData(nameof(Names))]
    public void A_filter_compiles_to_its_query(string name) =>
        Assert.Equal(_cases[name].Query, InstanceFilters.ToQuery(_cases[name].Filter));

    public static TheoryData<string> Names => [.. _cases.Keys];

    private static readonly Dictionary<string, (InstanceFilter Filter, string Query)> _cases = new(StringComparer.Ordinal)
    {
        ["Recipe = 'workflow.step'"] = (new InstanceFilter(Recipe: "workflow.step"), "Recipe = 'workflow.step'"),
        ["State = 'parked'"] = (new InstanceFilter(State: "parked"), "State = 'parked'"),
        ["NeedsYou = true"] = (new InstanceFilter(NeedsYou: true), "NeedsYou = true"),
        ["NeedsYou = false"] = (new InstanceFilter(NeedsYou: false), "NeedsYou = false"),
        ["Item = 'ITM-1'"] = (new InstanceFilter(Item: "ITM-1"), "Item = 'ITM-1'"),
        ["Subject = 'fact:spec'"] = (new InstanceFilter(Subject: "fact:spec"), "Subject = 'fact:spec'"),
        ["Urgency = 'incident'"] = (new InstanceFilter(Urgency: RecipeUrgency.Incident), "Urgency = 'incident'"),
        ["Urgency = 'triage'"] = (new InstanceFilter(Urgency: RecipeUrgency.Triage), "Urgency = 'triage'"),
        ["(Urgency = 'work' OR Urgency IS NULL)"] = (new InstanceFilter(Urgency: RecipeUrgency.Work), "(Urgency = 'work' OR Urgency IS NULL)"),
        ["(Root = true OR Root IS NULL)"] = (new InstanceFilter(Root: true), "(Root = true OR Root IS NULL)"),
        ["Root = false"] = (new InstanceFilter(Root: false), "Root = false"),
        ["Recipe = 'it\\'s a \\\\ recipe'"] = (new InstanceFilter(Recipe: "it's a \\ recipe"), "Recipe = 'it\\'s a \\\\ recipe'"),
        ["Recipe = 'workflow.step' AND State = 'produce' AND NeedsYou = true AND (Urgency = 'work' OR Urgency IS NULL) AND Item = 'ITM-1' AND Subject = 'fact:spec' AND (Root = true OR Root IS NULL)"] = (new InstanceFilter("workflow.step", "produce", NeedsYou: true, RecipeUrgency.Work, "ITM-1", "fact:spec", Root: true), "Recipe = 'workflow.step' AND State = 'produce' AND NeedsYou = true AND (Urgency = 'work' OR Urgency IS NULL) AND Item = 'ITM-1' AND Subject = 'fact:spec' AND (Root = true OR Root IS NULL)"),
    };
}
