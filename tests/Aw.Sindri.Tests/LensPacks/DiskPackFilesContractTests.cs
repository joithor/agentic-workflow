using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The disk adapter over a scratch directory, held to the same contract.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskPackFilesContractTests : PackFilesContract, IDisposable
{
    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("lens-packs-");

    protected override string Root => _scratch.FullName;

    public void Dispose() => _scratch.Delete(recursive: true);

    private protected override IOutsidePackFiles Create() => new DiskPackFiles();

    protected override void Seed(string id, string path, string text)
    {
        var file = Path.Combine(Root, "lenses", id, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);   // why: the path has a directory
        File.WriteAllText(file, text);
    }
}
