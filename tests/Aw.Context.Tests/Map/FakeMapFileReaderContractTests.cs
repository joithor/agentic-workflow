using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeMapFileReaderContractTests : MapFileReaderContract
{
    private readonly FakeMapFileReader _reader = new();

    protected override IMapFileReader CreateReader() => _reader;

    protected override string Place(string name, string repo)
    {
        var path = "/maps/" + name;
        _reader.Put(path, new MapDocument(1, MapDocumentKind.Authored, Context.Core.Engine.ContextRepoName.From(repo), []), repo);
        return path;
    }

    protected override string InWorktree(string name) => FakeMapFileReader.WorktreeRoot + "/" + name;
}
