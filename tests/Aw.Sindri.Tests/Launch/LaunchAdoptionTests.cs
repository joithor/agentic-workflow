using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Doctor's adoption line (launcher spec section 9).</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchAdoptionTests
{
    [Fact]
    public void No_started_launch_says_so()
    {
        Assert.Equal("no opens in the last 30 days", LaunchAdoption.Line([]));
    }
}
