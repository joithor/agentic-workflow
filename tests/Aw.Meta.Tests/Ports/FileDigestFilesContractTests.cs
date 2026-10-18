using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class FileDigestFilesContractTests : DigestFilesContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override (IDigestFiles Files, Func<string, string, DateTimeOffset, string> Put) Create() =>
        (new FileDigestFiles(), (name, content, modifiedAt) =>
        {
            var path = Path.Combine(_dir.Path, name);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, modifiedAt.UtcDateTime);
            return path;
        }
    );

    public void Dispose() => _dir.Dispose();
}
