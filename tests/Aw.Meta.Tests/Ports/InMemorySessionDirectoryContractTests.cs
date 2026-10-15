using Aw.Kernel.Testing;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemorySessionDirectoryContractTests : SessionDirectoryContract
{
    protected override ISessionDirectory Create(IReadOnlyList<LiveSession> alive, IReadOnlyList<LiveSession> dead)
    {
        var directory = new InMemorySessionDirectory();
        foreach (var session in alive)
        {
            directory.Add(session);
        }

        return directory;
    }
}
