using Aw.Kernel.Testing;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.CancelJob;

/// <summary>The red test of the CancelJob slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class CancelJobTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CancelJob_appends_job_cancelled_and_trips_the_token()
    {
        var result = await new CancelJobService().Handle(A.CancelJobRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
