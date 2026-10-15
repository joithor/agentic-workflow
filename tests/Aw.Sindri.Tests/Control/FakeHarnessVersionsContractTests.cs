using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class FakeHarnessVersionsContractTests : HarnessVersionsContract
{
    private readonly FakeHarnessVersions _fake = new();
    private int _next;

    protected override IHarnessVersions Create() => _fake;

    protected override string Installed(string output, string version)
    {
        var command = string.Create(CultureInfo.InvariantCulture, $"tool-{++_next}");
        return version.Length == 0 ? command : Install(command, version);
    }

    protected override string Failing() => "failing";

    protected override string Missing() => "missing";

    protected override string Noisy(string version) => Install("noisy", version);

    private string Install(string command, string version)
    {
        _ = _fake.With(command, version);
        return command;
    }
}
