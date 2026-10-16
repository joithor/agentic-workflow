using Aw.Kernel.Testing;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ResumeService;

/// <summary>The red test of the ResumeService slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class ResumeServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResumeService_appends_service_resumed_for_the_named_service()
    {
        var result = await new ResumeServiceService().Handle(A.ResumeServiceRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
