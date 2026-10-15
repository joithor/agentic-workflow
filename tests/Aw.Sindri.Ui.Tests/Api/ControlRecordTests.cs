using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The app follows only an endpoint record on this machine (UI spec revision table, "Security LOWs": loopback-only control.json).</summary>
[Lane(TestLane.Unit)]
public sealed class ControlRecordTests
{
    [Theory]
    [InlineData("http://127.0.0.1:7410/", true)]
    [InlineData("http://[::1]:7410/", true)]
    [InlineData("http://localhost:7410/", true)]
    [InlineData("http://example.com:7410/", false)]
    [InlineData("http://10.0.0.5:7410/", false)]
    public void Only_a_loopback_address_is_on_this_machine(string url, bool local) =>
        Assert.Equal(local, new ControlRecord(new Uri(url), 4242, 7).IsOnThisMachine);
}
