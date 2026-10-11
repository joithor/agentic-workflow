using Aw.Evidence.Core.Browser;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The Playwright adapter against the fixture site's home page, which is the contract page.</summary>
[Lane(TestLane.Heavy)]
[Collection(SharedBrowser.Name)]
public sealed class PlaywrightSessionContractTests(BrowserFixture fixture) : BrowserSessionContract
{
    protected override async Task<IBrowserSession> OpenContractPageAsync()
    {
        var session = await fixture.OpenAsync();
        var opened = await session.GotoAsync("/", PassingWait, CancellationToken.None);
        Assert.True(opened.IsSuccess, opened.IsError ? opened.Error.Message : null);
        return session;
    }
}
