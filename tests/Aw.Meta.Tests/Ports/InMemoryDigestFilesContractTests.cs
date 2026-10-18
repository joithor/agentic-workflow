using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryDigestFilesContractTests : DigestFilesContract
{
    protected override (IDigestFiles Files, Func<string, string, DateTimeOffset, string> Put) Create()
    {
        var fake = new InMemoryDigestFiles();
        return (fake, (name, content, modifiedAt) =>
        {
            fake.Put("/d/" + name, content.Length, modifiedAt);
            return "/d/" + name;
        }
        );
    }
}
