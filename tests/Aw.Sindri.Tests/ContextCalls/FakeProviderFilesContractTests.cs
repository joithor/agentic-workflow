using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

[Lane(TestLane.Unit)]
public sealed class FakeProviderFilesContractTests : ProviderFilesContract
{
    protected override IProviderFiles Create(IReadOnlyDictionary<ProviderFile, string> files)
    {
        var fake = new FakeProviderFiles();
        foreach (var (file, text) in files)
        {
            _ = fake.With(file, text);
        }

        return fake;
    }

    protected override IProviderFiles CreateUnreadable(ProviderFile file) => new FakeProviderFiles().Unreadable(file);
}
