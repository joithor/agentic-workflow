using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The terminals.json adapter passes the terminal names contract on the shipped file.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalProcessNamesContractTests : TerminalNamesContract
{
    protected override ITerminalNames Create() => TerminalProcessNames.Embedded();

    protected override ITerminalNames CreateEmpty() => TerminalProcessNames.Parse(Encoding.UTF8.GetBytes("not json"));
}
