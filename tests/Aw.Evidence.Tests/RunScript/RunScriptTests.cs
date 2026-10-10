using Aw.Evidence.Features.RunScript;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.RunScript;

public sealed class RunScriptTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task RunScript_writes_a_summary_with_every_step_passed()
    {
        var service = new RunScriptService();
        var request = A.RunScriptRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert writes_a_summary_with_every_step_passed");
    }
}
