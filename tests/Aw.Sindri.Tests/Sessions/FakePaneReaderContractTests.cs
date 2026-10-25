using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory pane reader passes the same contract as the tmux adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakePaneReaderContractTests : PaneReaderContract
{
    protected override string KnownSocket => "/tmp/tmux-1000/known";

    protected override string KnownPane => "%4";

    protected override string UnknownSocket => "/tmp/tmux-1000/none";

    protected override IPaneReader Create() => new FakePaneReader().With(KnownSocket, KnownPane, 410, "hello\n");
}
