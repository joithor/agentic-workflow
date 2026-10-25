using System.Globalization;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Control;

/// <summary>The '--version' reader against small scripts in a temp directory, run by their absolute paths.</summary>
[Lane(TestLane.Integration)]
[UnsupportedOSPlatform("windows")]
public sealed class CommandHarnessVersionsContractTests : HarnessVersionsContract, IDisposable
{
    private readonly TempHome _home = new();
    private int _next;

    public void Dispose() => _home.Dispose();

    protected override IHarnessVersions Create() => new CommandHarnessVersions();

    protected override string Installed(string output, string version) => Script($"echo '{output}'");

    protected override string Failing() => Script("echo 1.0.0; exit 1");

    protected override string Missing() => Path.Combine(_home.Path, "not-installed");

    protected override string Noisy(string version) => Script($"head -c 300000 /dev/zero | tr '\\0' x >&2; echo {version}");

    private string Script(string body)
    {
        var path = Path.Combine(_home.Path, string.Create(CultureInfo.InvariantCulture, $"tool-{++_next}"));
        return ExecutableScript.Write(path, body + "\n");
    }
}
