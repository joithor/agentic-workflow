using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.SetupWatch;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The 'setup' built-in service's shell (setup-once spec §5): it runs without a profile entry, so the services list shows it and PauseService refuses it; it has no steps yet.</summary>
public sealed class SetupServiceTests
{
    [Fact, Trait("Lane", "unit")]
    public void It_is_a_built_in_named_setup_with_a_one_minute_backstop()
    {
        Assert.Equal("setup", SetupService.BuiltIn.Name.Value);
        Assert.Equal(new Cadence(TimeSpan.FromSeconds(60)), SetupService.BuiltIn.Cadence);
        Assert.Equal(ServiceName.From("setup"), SetupService.ServiceId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tick_with_no_steps_yet_is_quiet()
    {
        var service = new SetupService();

        var ran = await service.Run(new ScheduledRun(Grant: default!, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);

        Assert.True(ran.Value.Idle);
        Assert.Equal(SetupService.ServiceId, service.Identity());
    }
}
