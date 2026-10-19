using System.Text;
using Aw.Sindri.Core.Workflow.Engine.Rules;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The keys of a report's findings (engine P8 D10): a list of {file, check, message}, normalized before it is hashed.</summary>
public sealed class FindingKeysTests
{
    private static IReadOnlyList<string>? Of(string json) => FindingKeys.Of(Encoding.UTF8.GetBytes(json));

    [Fact, Trait("Lane", "unit")]
    public void Messages_are_normalized_before_hashing()
    {
        var a = Of("""[{"file":"src\\A.cs","check":"build","message":"CS0103:   The name  'x' does not exist\n"}]""");
        var b = Of("""[{"file":"src/A.cs ","check":"build","message":"cs0103: the name 'x' does not exist"}]""");

        Assert.NotNull(a);
        Assert.Equal(a, b);
        _ = Assert.Single(a);
    }

    [Fact, Trait("Lane", "unit")]
    public void Keys_are_sorted_and_distinct()
    {
        var keys = Of("""[{"file":"b","check":"c","message":"m"},{"file":"a","check":"c","message":"m"},{"file":"a","check":"c","message":"M"}]""");

        Assert.Equal(2, keys?.Count);
        Assert.Equal(keys!.Order(StringComparer.Ordinal), keys, StringComparer.Ordinal); // why: the Assert above fails first when null
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("not json")]
    [InlineData("\"a string\"")]
    [InlineData("""{"file":"a","check":"c","message":"m"}""")]
    [InlineData("""[{"file":"a","check":"c"}]""")]
    [InlineData("""[{"file":"a","check":"c","message":3}]""")]
    [InlineData("""["a finding"]""")]
    public void Any_other_shape_has_no_keys(string json) => Assert.Null(Of(json));

    [Fact, Trait("Lane", "unit")]
    public void An_empty_list_has_no_findings() => Assert.Empty(Of("[]")!);   // why: [] is a list, so the keys are the empty list
}
