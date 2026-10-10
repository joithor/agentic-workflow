using Aw.BugFix.Features.Init;
using Aw.BugFix.Testing;

namespace Aw.BugFix.Tests.Init;

public sealed class InitTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Init_writes_intake_state_for_a_valid_ticket()
    {
        var service = new InitService();
        var request = A.InitRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert writes_intake_state_for_a_valid_ticket");
    }
}
