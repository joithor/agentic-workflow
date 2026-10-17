using Aw.Judge.Core.Providers;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class ChooseQuestionTests
{
    /// <summary>A valid choose input: three choices over one fact; the knobs build the invalid ones.</summary>
    internal static string Input(string choices = """[{"name":"ui","criterion":"only UI files changed"},{"name":"api","criterion":"an HTTP route changed"},{"name":"db","criterion":"a migration changed"}]""", string prompt = "Which evidence does this change need?", string? threshold = null)
    {
        var extra = threshold is null ? "" : $",\"threshold\":{threshold}";
        return $$"""{"prompt":{{System.Text.Json.JsonSerializer.Serialize(prompt)}},"state":{"diff":"M src/Button.tsx"},"choices":{{choices}}{{extra}}}""";
    }

    [Fact]
    public async Task Choose_is_a_catalog_question()
    {
        var cli = Cli(ContentClass.Brief).Decides("ui", 0.9);

        var outcome = await new QuestionRun().AskByName("choose", Input(), cli);

        _ = AssertModel("ui", outcome);
    }
}
