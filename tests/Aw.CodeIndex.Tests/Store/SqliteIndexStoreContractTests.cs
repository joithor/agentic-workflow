using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Store;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Lane(TestLane.Integration)]
public sealed class SqliteIndexStoreContractTests : IndexStoreContract, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-codeindex-store-").FullName;

    private string Indexes => Path.Combine(_root, "code-index");

    protected override IIndexStore Create() => new SqliteIndexStore(Indexes, new FakeTimeProvider(BuiltAt), new InProcessCodeIndexLock());

    protected override void Corrupt(IIndexStore store, RepoName repo)
    {
        var ulid = File.ReadAllText(Path.Combine(Indexes, $"{repo.Value}.current")).Trim();
        File.WriteAllBytes(Path.Combine(Indexes, $"{repo.Value}.{ulid}.db"), [.. Enumerable.Repeat((byte)'x', 8192)]);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
