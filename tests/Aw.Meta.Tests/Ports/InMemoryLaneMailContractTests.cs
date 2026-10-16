using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaneMailContractTests : LaneMailContract
{
    protected override ValueTask<(ILaneMail Mail, IMessageStore Messages)> Create()
    {
        var messages = new InMemoryMessageStore();
        return ValueTask.FromResult<(ILaneMail, IMessageStore)>((new InMemoryLaneMail(messages), messages));
    }
}
