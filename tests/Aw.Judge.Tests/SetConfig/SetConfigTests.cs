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
        var result = await Set(A.SetConfigRequest().Threshold("wake-gate", 0.85));

        Assert.True(result.IsSuccess);
        Assert.Equal("wake-gate=True/0.85/-; providers=[codex-cli]|False; promptSort=False||2|-", ConfigText.Of(Saved()));
        Assert.Same(_store.Stored, result.Value.Config);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetConfig_sets_enabled_on_a_new_question_from_the_defaults(bool enabled)
    {
        _ = await Set(A.SetConfigRequest().Enabled("ask-check", enabled));

        Assert.Equal(new QuestionConfig(enabled, 0.7), Saved().Questions["ask-check"]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public async Task SetConfig_refuses_a_threshold_outside_0_to_1_and_saves_nothing(double threshold)
    {
        var before = _store.Stored;

        var result = await Set(A.SetConfigRequest().Threshold("wake-gate", threshold));

        Assert.Equal(JudgeErrors.Invalid.Code, result.Error.Code);
        Assert.Same(before, _store.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_validator_needs_a_question_and_exactly_one_setting()
    {
        var validator = new SetConfigValidator();

        Assert.True(validator.Validate(new SetConfigRequest("q", Enabled: true)).IsSuccess);
        Assert.True(validator.Validate(new SetConfigRequest("", Enabled: true)).IsError);
        Assert.True(validator.Validate(new SetConfigRequest("q")).IsError);
        Assert.True(validator.Validate(new SetConfigRequest("q", Enabled: true, Threshold: 0.5)).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetConfig_refuses_to_overwrite_an_invalid_stored_config()
    {
        _store.InvalidReason = "invalid config config.json: not JSON";
        var before = _store.Stored;

        var result = await Set(A.SetConfigRequest().Threshold("wake-gate", 0.5));

        Assert.Equal(JudgeErrors.ConfigMalformed.Code, result.Error.Code);
        Assert.Equal("invalid config config.json: not JSON; fix or delete it, then set again", result.Error.Message);
        Assert.Same(before, _store.Stored);
    }

    private Task<Kernel.Result<SetConfigResponse>> Set(SetConfigRequestBuilder request) =>
        new SetConfigService(_store).Handle(request.Build(), CancellationToken.None).AsTask();

    private JudgeConfig Saved() => _store.Stored ?? throw new InvalidOperationException("nothing saved");
}
