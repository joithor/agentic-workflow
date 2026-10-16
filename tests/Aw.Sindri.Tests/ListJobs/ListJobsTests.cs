using Aw.Kernel.Testing;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListJobs;

/// <summary>The red test of the ListJobs slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class ListJobsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListJobs_lists_jobs_newest_first_filtered_by_state_and_kind()
    {
        var result = await new ListJobsService().Handle(A.ListJobsRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
