using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

[Lane(TestLane.Unit)]
public sealed class InMemoryPackFilesContractTests : PackFilesContract
{
    private readonly InMemoryPackFiles _files = new();

    protected override string Root => "/packs";

    private protected override IOutsidePackFiles Create() => _files;

    protected override void Seed(string id, string path, string text) => _files.Put(Root, id, path, text);
}
