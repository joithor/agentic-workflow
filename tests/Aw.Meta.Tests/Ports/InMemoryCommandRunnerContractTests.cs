using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryCommandRunnerContractTests : CommandRunnerContract
{
    protected override ICommandRunner Create() => new InMemoryCommandRunner();

    protected override IReadOnlyList<string> Prints(string text) => InMemoryCommandRunner.Prints(text);

    protected override IReadOnlyList<string> Exits(int code) => InMemoryCommandRunner.Exits(code);

    protected override IReadOnlyList<string> Hangs() => InMemoryCommandRunner.Hangs();

    protected override IReadOnlyList<string> Missing() => InMemoryCommandRunner.Missing();
}
