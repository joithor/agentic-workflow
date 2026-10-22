using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The in-memory changed-paths port, held to the same contract as the git adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeChangedPathsContractTests : ChangedPathsContract
{
    protected override ValueTask<(IChangedPaths Paths, string Checkout, string BaseRef, string Head)> Create() =>
        ValueTask.FromResult<(IChangedPaths, string, string, string)>((new FakeChangedPaths().With("/repo", "base1", "head1", "src/a.cs", "docs/guide/b.md", "README.md", "src/a.cs"), "/repo", "base1", "head1"));
}
