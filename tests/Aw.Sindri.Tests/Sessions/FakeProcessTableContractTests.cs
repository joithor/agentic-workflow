using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory process table passes the same contract as the /proc adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeProcessTableContractTests : ProcessTableContract
{
    protected override IProcessTable Create() => new FakeProcessTable()
        .With(SessionData.Proc(30, 1, "claude"), new Dictionary<string, string>(StringComparer.Ordinal) { ["TMUX"] = "/tmp/tmux-1000/t1,1,0", ["ANTHROPIC_API_KEY"] = PlantedToken })
        .With(SessionData.Proc(10, 1, "bash"))
        .With(SessionData.Proc(20, 1, "bash"));
}
