using Aw.Kernel.Testing;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PauseService;

/// <summary>The red test of the PauseService slice (API P6 contract): it names the behavior; the service is a stub until its unit.</summary>
[Lane(TestLane.Unit)]
public sealed class PauseServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PauseService_appends_service_paused_for_the_named_service()
    {
        var result = await new PauseServiceService().Handle(A.PauseServiceRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }
}
