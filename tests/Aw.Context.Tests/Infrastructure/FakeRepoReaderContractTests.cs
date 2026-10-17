using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class FakeRepoReaderContractTests : RepoReaderContract
{
    protected override IRepoReader CreateReader(out ContextRepo repo, out string commit)
    {
        repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");
        commit = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";
        return new FakeRepoReader(repo, commit, Committed);
    }

    protected override IRepoReader CreateChangeReader(out ContextRepo repo, out ChangeWorld world)
    {
        repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");
        (world, var reader) = ChangeWorlds.Fake(repo);
        return reader;
    }
}
