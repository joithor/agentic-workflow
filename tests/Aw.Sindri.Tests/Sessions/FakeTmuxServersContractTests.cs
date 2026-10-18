using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory tmux servers pass the same contract as the tmux adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeTmuxServersContractTests : TmuxServersContract
{
    protected override string KnownSocket => "/tmp/tmux-1000/known";

    protected override string UnknownSocket => "/tmp/tmux-1000/none";

    protected override ITmuxServers Create() => new FakeTmuxServers().With(KnownSocket,
        new TmuxServerView([new TmuxPane(410, "api", 0, 0, "%0")], [new TmuxClient(301, "/dev/pts/1", "api")]));
}
