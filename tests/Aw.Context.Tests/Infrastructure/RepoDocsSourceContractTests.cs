using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class RepoDocsSourceContractTests : ContextSourceContract<IContextSource>, IDisposable
{
    private readonly TempHome _home = new();
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose()
    {
        _home.Dispose();
        _repo.Dispose();
    }

    protected override async Task<IContextSource> Create()
    {
        var repo = new ContextRepo("split-panes", _repo.Path);
        _ = (await _home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        return new RepoDocsSource([repo], _home.Database, _home.Git, _home.Clock);
    }
}
