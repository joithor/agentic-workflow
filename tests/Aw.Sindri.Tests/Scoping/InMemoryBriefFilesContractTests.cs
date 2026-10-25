using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory brief reader honours the brief-file contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryBriefFilesContractTests : BriefFilesContract
{
    protected override IBriefFiles Create(IReadOnlyDictionary<string, string> files, out string root)
    {
        root = "/repo";
        var reader = new InMemoryBriefFiles();
        foreach (var (path, text) in files)
        {
            _ = reader.With(path, text);
        }

        return reader;
    }
}
