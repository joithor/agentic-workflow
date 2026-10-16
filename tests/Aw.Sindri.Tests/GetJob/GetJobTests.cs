using Aw.Kernel.Testing;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetJob;

/// <summary>The red test of the GetJob slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class GetJobTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetJob_returns_the_job_with_its_actions_and_cli_while_running()
    {
        var result = await new GetJobService().Handle(A.GetJobRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
