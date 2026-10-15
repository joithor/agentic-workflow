using System.Text.Json;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class ExpectationCheckTests
{
    private static Expectation Expecting(string json) => JsonSerializer.Deserialize(json, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Expectation>)TestJson.Options.GetTypeInfo(typeof(Expectation)))!;

    private static CheckedExpectation Check(string expectation, int code, string output, string? golden = null) =>
        ExpectationCheck.Evaluate(1, Expecting(expectation), code, output, golden);

    [Fact, Trait("Lane", "unit")]
    public void A_matching_status_and_no_other_assertions_counts_one_passing_assertion()
    {
        var checkedExpectation = Check("""{"status":200}""", 200, string.Empty);

        Assert.Equal((1, 0, null), (checkedExpectation.Count, checkedExpectation.Failures.Count, checkedExpectation.FirstFailedOrdinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wrong_exit_code_names_both_codes()
    {
        var failure = Assert.Single(Check("""{"exitCode":0}""", 3, string.Empty).Failures);

        Assert.Equal((1, null, "exit code 0", "exit code 3"), (failure.Step, failure.Location, failure.Expected, failure.Actual));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("1", """{"n":1.0}""", true)]
    [InlineData("1", """{"n":2}""", false)]
    [InlineData("""{"a":1,"b":[1,2]}""", """{"n":{"b":[1,2],"a":1}}""", true)]
    [InlineData("""{"a":1}""", """{"n":{"a":1,"c":2}}""", false)]
    [InlineData("[1,2]", """{"n":[2,1]}""", false)]
    [InlineData("null", """{"n":null}""", true)]
    [InlineData("null", """{"n":0}""", false)]
    [InlineData("\"x\"", """{"n":"x"}""", true)]
    public void Equals_compares_json_values_not_text(string expected, string body, bool holds)
    {
        var result = Check($$"""{"status":200,"pointers":[{"pointer":"/n","equals":{{expected}}}]}""", 200, body);

        Assert.Equal(holds, result.Failures.Count == 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void Exists_false_fails_when_the_pointer_resolves_and_names_what_it_found()
    {
        var failure = Assert.Single(Check("""{"status":200,"pointers":[{"pointer":"/n","exists":false}]}""", 200, """{"n":5}""").Failures);

        Assert.Equal(("/n", "absent", "5"), (failure.Location, failure.Expected, failure.Actual));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pointer_that_does_not_resolve_is_missing()
    {
        var failure = Assert.Single(Check("""{"status":200,"pointers":[{"pointer":"/n","equals":1}]}""", 200, "{}").Failures);

        Assert.Equal("missing", failure.Actual);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"n":"abc-42"}""", "^abc-\\\\d+$", true)]
    [InlineData("""{"n":42}""", "^4", true)]
    [InlineData("""{"n":true}""", "^true$", true)]
    [InlineData("""{"n":"abc"}""", "^x", false)]
    [InlineData("""{"n":{"a":1}}""", "a", false)]
    public void Matches_reads_a_string_or_the_json_text_of_another_scalar(string body, string pattern, bool holds)
    {
        var result = Check($$"""{"status":200,"pointers":[{"pointer":"/n","matches":"{{pattern}}"}]}""", 200, body);

        Assert.Equal(holds, result.Failures.Count == 0);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_failed_ordinal_counts_the_status_first_then_each_pointer_then_the_golden()
    {
        var result = Check("""{"status":200,"pointers":[{"pointer":"/a","equals":1},{"pointer":"/b","equals":2}]}""", 200, """{"a":1,"b":3}""");

        Assert.Equal((3, 3), (result.Count, result.FirstFailedOrdinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"a": 1}""", """{"a":1}""", true)]
    [InlineData("plain\n", "plain\n", true)]
    [InlineData("plain", "other", false)]
    public void A_golden_equals_as_canonical_json_when_both_are_json_else_as_text(string golden, string output, bool holds)
    {
        var result = Check("""{"status":200,"golden":"g.json"}""", 200, output, golden);

        Assert.Equal(holds, result.Failures.Count == 0);
        Assert.Equal(2, result.Count);
    }
}
