using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.JobProbe;

/// <summary>'job-probe' is opt-in (API P6 unit I): the host has it, but only a profile that names it schedules it.</summary>
[Lane(TestLane.Integration)]
public sealed class JobProbeHostTests
{
    [Fact(Timeout = 60_000)]
    public async Task Job_probe_is_a_service_of_the_build_and_not_a_built_in()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        await using var scope = host.App.Services.CreateAsyncScope();

        Assert.Contains(scope.ServiceProvider.GetServices<IScheduledService>(), static s => s.Identity().Value == "job-probe");
        Assert.DoesNotContain(host.App.Services.GetServices<BuiltInService>(), static b => b.Name.Value == "job-probe");
    }
}
