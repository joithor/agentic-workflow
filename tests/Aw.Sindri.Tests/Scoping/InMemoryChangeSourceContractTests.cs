using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory change source honours the change contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryChangeSourceContractTests : ChangeSourceContract
{
    protected override IChangeSource Create(IReadOnlyDictionary<string, string> baseFiles, IReadOnlyList<ChangeSeedCommit> commits, out string checkout)
    {
        checkout = "/work/web";
        return new InMemoryChangeSource(baseFiles, commits);
    }
}
