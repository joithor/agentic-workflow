using Aw.Kernel.Testing;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RunService;

/// <summary>The red test of the RunService slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class RunServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunService_starts_a_job_that_runs_the_named_service_now()
    {
        var result = await new RunServiceService().Handle(A.RunServiceRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
