using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.SyncInbox;

public sealed class SyncInboxTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task SyncInbox_records_each_peer_message_once()
    {
        var service = new SyncInboxService();
        var request = A.SyncInboxRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert records_each_peer_message_once");
    }
}
