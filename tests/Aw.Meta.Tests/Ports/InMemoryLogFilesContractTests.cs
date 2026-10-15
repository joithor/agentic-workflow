using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryLogFilesContractTests : LogFilesContract
{
    protected override ILogFiles Create() => new InMemoryLogFiles();

    protected override string Seed(ILogFiles logs, string name, string text)
    {
        ((InMemoryLogFiles)logs).Put("/logs/" + name, text);
        return "/logs/" + name;
    }
}
