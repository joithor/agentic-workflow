using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>GET /setup: the six steps in their fixed order, each from its own source, and a source that can't be read fails only its own step.</summary>
public sealed class GetSetupTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetSetup_reports_every_step_in_order()
    {
        var service = new GetSetupService();

        var result = await service.Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal([SetupStepId.Repo, SetupStepId.Profile, SetupStepId.Recipes, SetupStepId.Mcp, SetupStepId.Map, SetupStepId.Onyx], result.Value.Steps.Select(static s => s.Id));
        Assert.All(result.Value.Steps, static s => Assert.Equal(SetupState.Done, s.State));
        Assert.Equal(_clock.GetUtcNow(), result.Value.AsOf);
    }
}
