using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Config;
using Aw.Judge.Features.SetConfig;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetConfig;

public sealed class SetConfigTests
{
    private readonly InMemoryJudgeConfigStore _store = new()
    {
        Stored = JudgeConfig.Default with
        {
            Providers = new ProvidersConfig(["codex-cli"], Jev: false),
            PromptSort = new PromptSortConfig(Enabled: false, BudgetMs: null, CooldownPrompts: 2, Scaffolds: null),
        },
    };

    [Fact, Trait("Lane", "unit")]
    public async Task SetConfig_sets_a_threshold_and_keeps_the_provider_and_prompt_sort_blocks()
    {
        var result = await Set("wake-gate", "threshold", "0.85");

        Assert.True(result.IsSuccess);
        Assert.Equal("wake-gate=True/0.85/-; providers=[codex-cli]|False; promptSort=False||2|-", ConfigText.Of(Saved()));
        Assert.Same(_store.Stored, result.Value.Config);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("yes", false)]
    public async Task SetConfig_enabled_is_true_only_for_the_word_true(string value, bool enabled)
    {
        _ = await Set("ask-check", "enabled", value);

        Assert.Equal(new QuestionConfig(enabled, 0.7), Saved().Questions["ask-check"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetConfig_reads_any_other_field_as_the_threshold_as_TS_does()
    {
        _ = await Set("ask-check", "thresh", " 0.5 ");

        Assert.Equal(new QuestionConfig(Enabled: true, Threshold: 0.5), Saved().Questions["ask-check"]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("high")]
    [InlineData("")]
    [InlineData("Infinity")]
    [InlineData("NaN")]
    public async Task SetConfig_refuses_a_threshold_that_isnt_a_finite_number_and_saves_nothing(string value)
    {
        var before = _store.Stored;

        var result = await Set("wake-gate", "threshold", value);

        Assert.Equal(JudgeErrors.NotANumber.Code, result.Error.Code);
        Assert.Equal($"not a number: {value}", result.Error.Message);
        Assert.Same(before, _store.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetConfig_refuses_to_overwrite_an_invalid_stored_config()
    {
        _store.InvalidReason = "invalid config config.json: not JSON";
        var before = _store.Stored;

        var result = await Set("wake-gate", "threshold", "0.5");

        Assert.Equal(JudgeErrors.ConfigMalformed.Code, result.Error.Code);
        Assert.Equal("invalid config config.json: not JSON; fix or delete it, then set again", result.Error.Message);
        Assert.Same(before, _store.Stored);
    }

    private Task<Kernel.Result<SetConfigResponse>> Set(string question, string field, string value) =>
        new SetConfigService(_store).Handle(A.SetConfigRequest().Setting(question, field, value).Build(), CancellationToken.None).AsTask();

    private JudgeConfig Saved() => _store.Stored ?? throw new InvalidOperationException("nothing saved");
}
