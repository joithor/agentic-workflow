using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FileRequest;

public sealed class FileRequestTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task FileRequest_from_session_appends_one_event_with_repo()
    {
        var service = new FileRequestService();
        var request = A.FileRequestRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert from_session_appends_one_event_with_repo");
    }
}
