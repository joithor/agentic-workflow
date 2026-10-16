using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionExit;

public sealed class SessionExitTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SessionExit_records_session_ended_with_the_exit_code()
    {
        var service = new SessionExitService();
        var request = A.SessionExitRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert records_session_ended_with_the_exit_code");
    }
}
