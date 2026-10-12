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

    protected override string Folder => Path.Combine(_home.Root, "context", "exports");

    protected override string Worktree => Path.Combine(_home.Root, "repo");

    public void Dispose() => _home.Dispose();

    protected override IExportStore CreateStore() => new FileExportStore(SettingsFromEnvironment.Read(_home.Environment), FixtureMap.Json);
}
