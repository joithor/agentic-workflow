using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class FileExportStoreContractTests : ExportStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public FileExportStoreContractTests()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Root, "repo", ".git"));
    }

    protected override string Folder => Path.Combine(Aw.Context.Infrastructure.Map.RealPath.Of(_home.Root)!, "context", "exports");   // why: the temp root exists, so it resolves

    protected override string Worktree => Path.Combine(_home.Root, "repo");

    public void Dispose() => _home.Dispose();

    protected override IExportStore CreateStore() => new FileExportStore(SettingsFromEnvironment.Read(_home.Environment), FixtureMap.Json);
}
