using Aw.Kernel.Testing;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-tree contract bound to the in-memory fake (unit lane).</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRepoTreeContractTests : RepoTreeContract
{
    protected override IRepoTree TreeWith(IReadOnlyDictionary<string, string> files) =>
        files.Aggregate(new InMemoryRepoTree(), (tree, f) => tree.With(f.Key, f.Value));
}
