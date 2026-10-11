using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class VisualCritiqueTests
{
    [Fact]
    public async Task Every_screenshot_goes_to_an_image_model_and_its_reasons_come_back()
    {
        var extra = new System.Text.Json.Nodes.JsonObject { ["reasons"] = new System.Text.Json.Nodes.JsonArray("button misaligned") };
        var cli = Cli(ContentClass.Image).Returns(new ProviderDecided("looks-off", 0.6, "model") { Extra = extra });

        var outcome = await new QuestionRun().Ask(
            VisualCritiqueQuestion.Definition, """{"afterScreenshot":"after.png","baselineScreenshot":"before.png","evidenceDir":"/tmp/ev"}""", cli);

        Assert.Equal("""{"reasons":["button misaligned"]}""", AssertModel("looks-off", outcome).Extra?.ToJsonString());
        Assert.Contains("Then look at before.png", Assert.Single(cli.Calls).Question.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_baseline_it_is_judged_on_its_own_and_a_text_only_model_is_never_asked()
    {
        var text = new Aw.Judge.Testing.ScriptedProvider(ProviderName.Jev, ContentClass.Brief).Decides("looks-right", 1);
        var cli = Cli(ContentClass.Image).Decides("looks-right", 0.4);

        var outcome = await new QuestionRun().Ask(VisualCritiqueQuestion.Definition, """{"afterScreenshot":"after.png","evidenceDir":"/tmp/ev"}""", text, cli);

        Assert.Equal(new EscalatedOutcome("below-threshold"), outcome);
        Assert.Empty(text.Calls);
        Assert.Contains("judge it on its own", Assert.Single(cli.Calls).Question.Prompt, StringComparison.Ordinal);
    }
}
