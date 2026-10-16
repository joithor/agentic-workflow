using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>The in-memory process scanner passes the same contract as the real one.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryProcessLocationsContractTests : ProcessLocationsContract
{
    private readonly InMemoryProcessLocations _fake = new();
    private int _directories;

    protected override IProcessLocations Create() => _fake;

    protected override int StartProcessIn(string directory) => _fake.StartIn(directory);

    protected override string NewDirectory() => "/fake/dir-" + (++_directories).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
