using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReadLedger;

public sealed class ReadLedgerTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ReadLedger_pages_public_envelopes_after_a_cursor_with_sanitized_payloads()
    {
        var service = new ReadLedgerService();
        var request = A.ReadLedgerRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert pages_public_envelopes_after_a_cursor_with_sanitized_payloads");
    }
}
