using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class SystemCommandRunnerContractTests : CommandRunnerContract
{
    protected override ICommandRunner Create() => new SystemCommandRunner();

    protected override IReadOnlyList<string> Prints(string text) => ["printf", "%s", text];

    protected override IReadOnlyList<string> Exits(int code) => ["sh", "-c", "exit " + code.ToString(CultureInfo.InvariantCulture)];

    protected override IReadOnlyList<string> Hangs() => ["sleep", "60"];

    protected override IReadOnlyList<string> Missing() => ["aw-meta-no-such-program"];
}
