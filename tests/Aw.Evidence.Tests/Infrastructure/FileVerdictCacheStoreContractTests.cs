using Aw.Evidence.Core.Visual;
using Aw.Evidence.Infrastructure.Files;
using Aw.Evidence.Infrastructure.Visual;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class FileVerdictCacheStoreContractTests : VerdictCacheStoreContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string Root => _root.Path;

    protected override IVerdictCacheStore Create() => new FileVerdictCacheStore(new FileRunFiles(), TestJson.Options);

    [Fact]
    public async Task A_malformed_manifest_is_an_empty_cache()
    {
        var path = _root.Combine("verdict-cache.json");
        await File.WriteAllTextAsync(path, "{ not json", TestContext.Current.CancellationToken);

        Assert.Empty((await Create().LoadAsync(path, TestContext.Current.CancellationToken)).Entries);
    }

    public void Dispose() => _root.Dispose();
}
