using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Injection;

/// <summary>The pure merge of the filled inputs into one injection plan (plan decision 3).</summary>
[Lane(TestLane.Unit)]
public sealed class InjectionPlansTests
{
    private static readonly BlockId _a = BlockId.From("input.test.a");
    private static readonly BlockId _b = BlockId.From("input.test.b");

    private static FilledInput Filled(BlockId input, int index, params string[] sections) =>
        new(input, index, [.. sections.Select(s => new InjectionEntry(s, input, 10, new string('a', 64)))]);

    [Fact]
    public void Merge_keeps_declaration_order()
    {
        var plan = InjectionPlans.Merge([Filled(_b, 2, "later"), Filled(_a, 1, "first", "second")]);

        Assert.Equal(["first", "second", "later"], plan.Entries.Select(static e => e.Section), StringComparer.Ordinal);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Merge_later_duplicate_wins_in_place_and_warns()
    {
        var plan = InjectionPlans.Merge([Filled(_a, 1, "shared", "only-a"), Filled(_b, 2, "shared")]);

        Assert.Equal(["shared", "only-a"], plan.Entries.Select(static e => e.Section), StringComparer.Ordinal);
        Assert.Equal((_b, _a), (plan.Entries[0].Input, plan.Entries[0].Replaced));
        Assert.Equal(new InjectionWarning("shared", _a, _b), Assert.Single(plan.Warnings));
    }

    [Fact]
    public void Merge_of_none_is_empty()
    {
        var plan = InjectionPlans.Merge([]);

        Assert.Empty(plan.Entries);
        Assert.Empty(plan.Warnings);
    }
}
