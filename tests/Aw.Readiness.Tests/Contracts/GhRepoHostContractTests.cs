using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-host contract bound to the gh adapter, with a stub gh script answering each scenario (integration
/// lane; no network).</summary>
[Lane(TestLane.Integration)]
public sealed class GhRepoHostContractTests : RepoHostContract, IDisposable
{
    private readonly TempDirectory _bin = new();

    protected override IRepoHost HostFor(HostScenario scenario)
    {
        var gh = scenario == HostScenario.Unreachable
            ? Path.Combine(_bin.Path, "no-such-gh")
            : StubGh.Write(_bin.Path, scenario, Now);
        var settings = SettingsFromEnvironment.Read(name => name == "AW_READINESS_GH" ? gh : null);
        return new GhRepoHost(settings);
    }

    public void Dispose() => _bin.Dispose();
}
