using Aw.CodeIndex.Core.Planning;

namespace Aw.CodeIndex.Tests.Planning;

public sealed class StructureStampTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_stamp_is_the_parser_id_and_eight_hex_digits()
    {
        var stamp = StructureStamp.Compute("roslyn-1", ["**/*Util.cs"], ["secret-\\d+"]);

        Assert.Matches("^roslyn-1:[0-9a-f]{8}$", stamp);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_inputs_give_the_same_stamp_in_any_order_with_repeats()
    {
        var one = StructureStamp.Compute("p", ["a", "b"], ["x", "y"]);
        var other = StructureStamp.Compute("p", ["b", "a", "a"], ["y", "x"]);

        Assert.Equal(one, other);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_parser_version_glob_or_scrub_pattern_changes_the_stamp()
    {
        var baseline = StructureStamp.Compute("p", ["a"], ["x"]);

        Assert.NotEqual(baseline, StructureStamp.Compute("q", ["a"], ["x"]), StringComparer.Ordinal);
        Assert.NotEqual(baseline, StructureStamp.Compute("p", ["a", "b"], ["x"]), StringComparer.Ordinal);
        Assert.NotEqual(baseline, StructureStamp.Compute("p", ["a"], ["x", "y"]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_glob_moved_into_the_scrub_list_is_a_different_stamp()
        => Assert.NotEqual(StructureStamp.Compute("p", ["a"], []), StructureStamp.Compute("p", [], ["a"]), StringComparer.Ordinal);
}
