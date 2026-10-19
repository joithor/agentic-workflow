using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Testing.History;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class FakeFileHistoryContractTests : FileHistoryContract
{
    protected override IFileHistory Create(out ContextRepo repo, out HistoryWorld world)
    {
        repo = HistoryWorlds.FakeRepo;
        world = HistoryWorlds.Fake();
        return new FakeFileHistory(repo, world);
    }

    protected override IFileHistory CreateShallow(out ContextRepo repo)
    {
        repo = HistoryWorlds.FakeRepo;
        return new FakeFileHistory(repo, HistoryWorlds.Fake(), shallow: true);
    }

    protected override IFileHistory CreateMissing(out ContextRepo repo)
    {
        repo = new ContextRepo(ContextRepoName.From("nowhere"), "/work/nowhere");
        return new FakeFileHistory(HistoryWorlds.FakeRepo, HistoryWorlds.Fake());
    }
}
