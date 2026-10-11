using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListCommands;

public sealed class ListCommandsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListCommands_lists_every_action_id_with_its_class()
    {
        var service = new ListCommandsService();
        var request = A.ListCommandsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_every_action_id_with_its_class");
    }
}
