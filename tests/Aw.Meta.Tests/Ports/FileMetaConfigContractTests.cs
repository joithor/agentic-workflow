using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class FileMetaConfigContractTests : MetaConfigContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override IMetaConfig Create() =>
        new FileMetaConfig(Path.Combine(_dir.Path, "meta", "team.json"), AwJson.CreateOptions(MetaJsonContext.Default, indented: true));

    public void Dispose() => _dir.Dispose();
}
