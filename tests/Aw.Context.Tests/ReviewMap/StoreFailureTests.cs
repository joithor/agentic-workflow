using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Review;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.ReviewMap;

/// <summary>A store that can't open answers SND-CONTEXT-004 from every S-4 slice and writes nothing.</summary>
public sealed class StoreFailureTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    public static TheoryData<string> Slices() =>
    [
        "build", "queue", "review", "export", "apply", "promote", "propose", "proposals", "lenses", "filemap", "symbol",
    ];

    private static string Code<T>(Result<T> result) => result.IsError ? result.Error.Code : "ok";

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Slices))]
    public async Task A_store_that_cant_open_is_SND_CONTEXT_004(string slice)
    {
        _world.Store.Failure = ContextErrors.StoreUnavailable.With("the store can't be opened");
        var scope = Drafts.Scope;
        var link = LinkId.From("edge:0123456789abcdef");
        var file = Path.Combine(FakeReviewFileStore.Folder, "x.json");
        _ = await _world.ReviewFiles.Write(file, "x.json", new ReviewFile("v1", []), MapWorld.None);
        var proposal = new ScopeProposal("run-1", ContextRepoName.From(FixtureMap.Repo), []);

        var code = slice switch
        {
            "build" => Code(await _world.Map.BuildMap(progress: null, scope, MapWorld.None)),
            "queue" => Code(await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)),
            "review" => Code(await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Accept, Link: link), scope, MapWorld.None)),
            "export" => Code(await _world.Map.ExportReview(path: null, scope, MapWorld.None)),
            "apply" => Code(await _world.Map.ApplyReviewFile(file, scope, MapWorld.None)),
            "promote" => Code(await _world.Map.Promote(scope, MapWorld.None)),
            "propose" => Code(await _world.Map.Propose(proposal, scope, MapWorld.None)),
            "proposals" => Code(await _world.Map.Proposals("run-1", scope, MapWorld.None)),
            "lenses" => Code(await _world.Engine.Lenses("feature:a", read: MapRead.Current, depth: null, scope, MapWorld.None)),
            "filemap" => Code(await _world.Engine.FileMap(new FileMapQuery(ContextRepoName.From(FixtureMap.Repo), "src/", UnmappedOnly: false, Draft: false), scope, MapWorld.None)),
            _ => Code(await _world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/a.ts"), scope, MapWorld.None)),
        };

        Assert.Equal(ContextErrors.StoreUnavailable.Code, code);
    }
}
