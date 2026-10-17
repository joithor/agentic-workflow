using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The step list is built and checked in one place; each step names its upstream and gets its stamp, and the response says what each step did.
public sealed class OnyxUpStepsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class StubStep(string name, string? after, OnyxStepOutcome outcome = OnyxStepOutcome.Ran) : IOnyxUpStep
    {
        public string Name => name;

        public string? After => after;

        public string Stamp(OnyxUpContext context) => name + ":" + (after is null ? "" : context.Upstream(after));

        public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<OnyxStepOutcome>>(outcome);
    }

    private static OnyxUpService Service(OnyxUpRig rig, params IOnyxUpStep[] steps)
        => new(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Pins, rig.Progress, steps: steps);

    [Fact, Trait("Lane", "unit")]
    public void The_steps_are_fetch_config_pull_start_each_after_the_one_before()
    {
        var rig = new OnyxUpRig();

        _ = rig.Service();   // composes, so the order is validated

        Assert.Equal(["fetch", "config", "pull", "start"], OnyxSteps.Order, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_list_out_of_order_missing_a_step_or_chained_wrongly_does_not_compose()
    {
        var rig = new OnyxUpRig();
        var fetch = new StubStep("fetch", after: null);
        var config = new StubStep("config", "fetch");
        var pull = new StubStep("pull", "config");
        var start = new StubStep("start", "pull");

        _ = Assert.Throws<InvalidOperationException>(() => Service(rig, config, fetch, pull, start));
        _ = Assert.Throws<InvalidOperationException>(() => Service(rig, fetch, config, start));
        _ = Assert.Throws<InvalidOperationException>(() => Service(rig, fetch, config, pull, new StubStep("start", "config")));
        _ = Service(rig, fetch, config, pull, start);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_upstream_stamp_is_an_error_not_an_empty_string()
    {
        var rig = new OnyxUpRig();
        var context = new OnyxUpContext(rig.Pins.Pins, rig.Paths, 3000, "linux/arm64");

        var error = Assert.Throws<InvalidOperationException>(() => context.Upstream("fetch"));

        Assert.Contains("fetch", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_response_carries_one_typed_outcome_per_step_in_order()
    {
        var rig = new OnyxUpRig();

        var first = (await rig.Up(Token)).Value;
        var second = (await rig.Up(Token)).Value;

        Assert.Equal(
            [new OnyxStepResult("fetch", OnyxStepOutcome.Ran), new OnyxStepResult("config", OnyxStepOutcome.Ran), new OnyxStepResult("pull", OnyxStepOutcome.Ran), new OnyxStepResult("start", OnyxStepOutcome.Ran)],
            first.Steps);
        Assert.All(second.Steps, static s => Assert.Equal(OnyxStepOutcome.Skipped, s.Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_needs_the_human_is_reported_as_such_and_is_recorded()
    {
        var rig = new OnyxUpRig();
        var service = Service(rig, new StubStep("fetch", after: null), new StubStep("config", "fetch"), new StubStep("pull", "config"), new StubStep("start", "pull", outcome: OnyxStepOutcome.NeedsHuman));

        var response = (await service.Handle(new OnyxUpRequest(), Token)).Value;

        Assert.Equal(OnyxStepOutcome.NeedsHuman, response.Steps[^1].Outcome);
        Assert.Contains("start", response.NeedsHuman, StringComparer.Ordinal);
    }
}
