using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ChooseConfidenceTests
{
    private static QuestionRef Choose { get; } = Questions.Text with { Name = "choose", Outputs = ["ui", "api"], AskConfidence = true };

    [Fact]
    public void Choose_schema_requires_confidence_in_strict_and_lax()
    {
        var lax = AnswerSchema.Lax(Choose);
        var strict = AnswerSchema.Strict(Choose);

        Assert.Equal("""["decision","confidence"]""", lax["required"]!.ToJsonString());
        Assert.Equal("""["decision","confidence"]""", strict["required"]!.ToJsonString());
        Assert.Equal("number", lax["properties"]!["confidence"]!["type"]!.GetValue<string>());
        Assert.Equal("""{"type":"number"}""", strict["properties"]!["confidence"]!.ToJsonString());   // why: required, so not nullable, and strict mode takes no range keywords
        Assert.Equal(0, lax["properties"]!["confidence"]!["minimum"]!.GetValue<int>());
        Assert.Equal(1, lax["properties"]!["confidence"]!["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void Other_questions_schema_is_unchanged()
    {
        Assert.Equal("""["decision"]""", AnswerSchema.Lax(Questions.Text)["required"]!.ToJsonString());
        Assert.Null(AnswerSchema.Lax(Questions.WithIndex)["properties"]!["confidence"]);
        Assert.Null(AnswerSchema.Strict(Questions.Text)["properties"]!["confidence"]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void The_confidence_bounds_0_and_1_are_accepted(string confidence)
    {
        var result = AgentAnswer.Read(JsonNode.Parse($$"""{"decision":"ui","confidence":{{confidence}}}"""), Choose);

        _ = Assert.IsType<ProviderDecided>(result);
    }

    [Fact]
    public void A_cli_confidence_below_threshold_is_undecided()
    {
        var result = AgentAnswer.Read(JsonNode.Parse("""{"decision":"ui","confidence":0.4}"""), Choose);

        var decided = Assert.IsType<ProviderDecided>(result);
        Assert.Equal(("ui", 0.4), (decided.Decision, decided.Confidence));
        Assert.Null(decided.Extra);   // why: confidence is the envelope's own field, never an extra
    }

    [Theory]
    [InlineData("""{"decision":"ui"}""")]
    [InlineData("""{"decision":"ui","confidence":1.5}""")]
    [InlineData("""{"decision":"ui","confidence":-0.1}""")]
    [InlineData("""{"decision":"ui","confidence":"high"}""")]
    public void A_choose_answer_without_a_usable_confidence_is_errored_not_certain(string answer)
        => Assert.Equal(new ProviderErrored("unparseable-answer"), AgentAnswer.Read(JsonNode.Parse(answer), Choose));

    [Fact]
    public void Other_questions_still_default_a_missing_confidence_to_certain()
    {
        var decided = Assert.IsType<ProviderDecided>(AgentAnswer.Read(JsonNode.Parse("""{"decision":"send"}"""), Questions.Text));

        Assert.Equal(1.0, decided.Confidence);
    }
}
