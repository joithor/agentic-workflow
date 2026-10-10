using Aw.Kernel.Testing;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-reader contract bound to the in-memory fake (unit lane).</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRepoReaderContractTests : RepoReaderContract
{
    protected override string ExistingRoot => "/repo";

    protected override string FileRoot => "/repo/README.md";

    protected override string MissingRoot => "/missing";

    protected override IRepoReader NewReader() => new InMemoryRepoReader().With("/repo", new InMemoryRepoTree().With("README.md", "x"));
}
