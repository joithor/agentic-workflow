using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionHeartbeat;

public sealed class SessionHeartbeatTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SessionHeartbeat_refuses_a_wrong_launch_secret()
    {
        var service = new SessionHeartbeatService();
        var request = A.SessionHeartbeatRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert refuses_a_wrong_launch_secret");
    }
}
