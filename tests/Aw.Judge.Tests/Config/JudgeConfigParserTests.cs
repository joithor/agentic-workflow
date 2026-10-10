using Aw.Judge.Core.Config;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.Config;

// config.json's reading rules (TS config.ts loadConfig): lenient, never fatal.
public sealed class JudgeConfigParserTests
{
    private static readonly string _defaults = ConfigText.Of(JudgeConfig.Default);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    [InlineData("""{"questions":[],"providers":{"jev":false}}""")]
    [InlineData("""{"questions":7}""")]
    public void A_file_that_isnt_an_object_or_has_a_bad_questions_block_is_the_default_config(string? text) =>
        Assert.Equal(_defaults, ConfigText.Of(JudgeConfigParser.Parse(text)));

    [Fact, Trait("Lane", "unit")]
    public void Questions_merge_over_the_default_wake_gate_and_drop_unknown_provider_names()
    {
        var config = JudgeConfigParser.Parse(
            """{"questions":{"ask-check":{"enabled":false,"threshold":0.6,"providers":["jev","gpt","rules"]},"rule-check":{"enabled":true,"threshold":0.9,"providers":["gpt"]},"turn-progress":{"enabled":true,"threshold":0.5,"providers":"jev"}}}""");

        Assert.Equal(
            "wake-gate=True/0.7/-; ask-check=False/0.6/[jev,rules]; rule-check=True/0.9/-; turn-progress=True/0.5/-; ",
            ConfigText.Of(config));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_with_wrong_typed_fields_gets_the_default_for_each()
    {
        var config = JudgeConfigParser.Parse("""{"questions":{"wake-gate":{"enabled":"yes","threshold":0.8},"x":{"threshold":"high"},"y":3}}""");

        Assert.Equal("wake-gate=True/0.8/-; x=True/0.7/-; ", ConfigText.Of(config));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_providers_block_keeps_known_agent_clis_and_a_boolean_jev()
    {
        Assert.Equal("providers=[codex-cli]|False", Providers("""{"providers":{"agentClis":["codex-cli","jev","x"],"jev":false}}"""));
        Assert.Equal("providers=[]|", Providers("""{"providers":{"agentClis":[]}}"""));
        Assert.Equal("", Providers("""{"providers":{"agentClis":"claude-cli","jev":"no"}}"""));
        Assert.Equal("", Providers("""{"providers":[]}"""));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_prompt_sort_block_clamps_the_budget_and_keeps_known_scaffolds()
    {
        Assert.Equal("promptSort=False|1050|2|brief:True,plan-first:False", PromptSort(
            """{"promptSort":{"enabled":false,"budgetMs":5000,"cooldownPrompts":2,"scaffolds":{"brief":true,"plan-first":false,"other":true,"bugfix":"yes"}}}"""));
        Assert.Equal("promptSort=|200||-", PromptSort("""{"promptSort":{"budgetMs":10,"cooldownPrompts":-1,"scaffolds":[]}}"""));
        Assert.Equal("promptSort=|||-", PromptSort("""{"promptSort":{}}"""));
        Assert.Equal("", PromptSort("""{"promptSort":"on"}"""));
    }

    private static string Providers(string text) => Block(text, "providers=");

    private static string PromptSort(string text) => Block(text, "promptSort=");

    private static string Block(string text, string prefix)
    {
        var all = ConfigText.Of(JudgeConfigParser.Parse(text));
        var start = all.IndexOf(prefix, StringComparison.Ordinal);
        return start < 0 ? "" : all[start..].Split("; ")[0];
    }
}
