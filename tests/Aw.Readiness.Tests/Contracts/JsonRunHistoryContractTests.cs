using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The run-history contract bound to the JSON-file adapter on a temp state directory (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class JsonRunHistoryContractTests : RunHistoryContract, IDisposable
{
    private readonly TempDirectory _state = new();

    protected override IRunHistory NewHistory() =>
        new JsonRunHistory(SettingsFromEnvironment.Read(name => name == "AW_STATE_DIR" ? _state.Path : null));

    public void Dispose() => _state.Dispose();
}
