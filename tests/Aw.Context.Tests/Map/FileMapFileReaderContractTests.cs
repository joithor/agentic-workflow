using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Testing.Map;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "integration")]
public sealed class FileMapFileReaderContractTests : MapFileReaderContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override IMapFileReader CreateReader() => new MapFileReader(FixtureMap.Json);

    protected override string Place(string name, string repo)
    {
        var path = Path.Combine(_home.Root, name);
        File.WriteAllBytes(path, AwJson.Serialize(new MapDocument(1, MapDocumentKind.Authored, Context.Core.Engine.ContextRepoName.From(repo), []), FixtureMap.Json));
        return path;
    }

    protected override string InWorktree(string name)
    {
        var repo = Path.Combine(_home.Root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var path = Path.Combine(repo, name);
        File.WriteAllText(path, "schemaVersion: 1\nkind: authored\nrepo: r\nnodes: []\n");
        return path;
    }
}
