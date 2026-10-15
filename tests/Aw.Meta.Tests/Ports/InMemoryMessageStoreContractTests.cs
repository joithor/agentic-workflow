using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryMessageStoreContractTests : MessageStoreContract
{
    protected override ValueTask<IMessageStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IMessageStore>(new InMemoryMessageStore());
}
