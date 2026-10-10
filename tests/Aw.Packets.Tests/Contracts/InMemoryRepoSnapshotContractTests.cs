using Aw.Kernel.Testing;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryRepoSnapshotContractTests : RepoSnapshotContract
{
    private const string Sha = "1111111111111111111111111111111111111111";

    protected override string NotARepository => "not-a-repo";

    protected override (IRepoSnapshot Snapshot, string Repo, string Sha) Create() =>
        (new InMemoryRepoSnapshot().With(Sha, CommitFiles).Unreadable("not-a-repo"), "repo", Sha);
}
