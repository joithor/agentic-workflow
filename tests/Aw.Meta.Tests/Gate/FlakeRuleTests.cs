using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class FlakeRuleTests
{
    private static readonly RerunOutcome _pass = new(Passed: true);

    [Fact, Trait("Lane", "unit")]
    public void A_test_that_passes_alone_is_a_flake() =>
        Assert.Equal(FlakeVerdict.Flake, FlakeRule.Classify(_pass, new RerunOutcome(Passed: true), "Assert.Equal failed"));

    [Fact, Trait("Lane", "unit")]
    public void A_test_that_fails_the_same_way_on_main_is_a_flake() =>
        Assert.Equal(
            FlakeVerdict.Flake,
            FlakeRule.Classify(new RerunOutcome(Passed: false, "boom  at\nline 3"), new RerunOutcome(Passed: false, "boom at line 3 "), "boom at line 3"));

    [Fact, Trait("Lane", "unit")]
    public void A_test_that_fails_alone_and_passes_on_main_is_real() =>
        Assert.Equal(FlakeVerdict.Real, FlakeRule.Classify(new RerunOutcome(Passed: false, "boom"), _pass, "boom"));

    [Fact, Trait("Lane", "unit")]
    public void A_test_that_fails_differently_on_main_is_real() =>
        Assert.Equal(
            FlakeVerdict.Real,
            FlakeRule.Classify(new RerunOutcome(Passed: false, "boom"), new RerunOutcome(Passed: false, "other"), "boom"));
}
