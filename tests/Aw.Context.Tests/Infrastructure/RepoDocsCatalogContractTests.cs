using Aw.Context.Core.Docs;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class RepoDocsCatalogContractTests : SourceCatalogContract<ISourceCatalog>, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override ISourceCatalog Create() => new RepoDocsCatalog(_home.Database, _home.Git, _home.Scrubber, _home.Clock);
}
