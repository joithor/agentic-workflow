using System.Text.Json;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Tests.Model;

public sealed class CoverageTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(Coverage.Full, "\"full\"")]
    [InlineData(Coverage.Partial, "\"partial\"")]
    [InlineData(Coverage.Unavailable, "\"unavailable\"")]
    public void Coverage_serializes_as_its_string_name(Coverage coverage, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(coverage));
        Assert.Equal(coverage, JsonSerializer.Deserialize<Coverage>(json));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_partial_row_marks_the_whole_answer_partial()
        => Assert.Equal(Coverage.Partial, CallCoverage.Of(Coverage.Full, [Edge(Coverage.Full), Edge(Coverage.Partial)]));

    [Fact, Trait("Lane", "unit")]
    public void An_empty_answer_for_a_partial_language_is_partial()
        => Assert.Equal(Coverage.Partial, CallCoverage.Of(Coverage.Partial, []));

    [Fact, Trait("Lane", "unit")]
    public void An_unmeasured_language_is_unavailable_whatever_the_rows()
        => Assert.Equal(Coverage.Unavailable, CallCoverage.Of(Coverage.Unavailable, [Edge(Coverage.Partial)]));

    [Fact, Trait("Lane", "unit")]
    public void Full_rows_in_a_full_language_stay_full()
        => Assert.Equal(Coverage.Full, CallCoverage.Of(Coverage.Full, [Edge(Coverage.Full)]));

    private static CallEdge Edge(Coverage coverage)
        => new(Level: 1, Other: null, "Helper", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 3, Candidates: 1, Coverage: coverage);
}
