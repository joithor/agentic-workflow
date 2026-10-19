using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>The in-memory host honours the same contract as the real one.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeProviderHostContractTests : ProviderHostContract
{
    private readonly FakeProviderHost _host = new();

    protected override string Root => "/scratch/repo";

    protected override IProviderHost CreateHost() => _host;

    protected override Task Commit(string relativePath, string text)
    {
        var path = Path.Combine(Root, relativePath);
        _host.Files[path] = text;
        _ = _host.WithTracked(path);
        return Task.CompletedTask;
    }
}
