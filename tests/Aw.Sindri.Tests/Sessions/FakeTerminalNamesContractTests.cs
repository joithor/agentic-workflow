using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The in-memory terminal rows pass the same contract as the terminals.json adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeTerminalNamesContractTests : TerminalNamesContract
{
    protected override ITerminalNames Create() => new FakeTerminalNames();

    protected override ITerminalNames CreateEmpty() => new FakeTerminalNames([]);
}
