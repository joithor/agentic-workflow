using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryDigestFilesContractTests : DigestFilesContract
{
    protected override (IDigestFiles Files, Func<string, string, string> Put) Create()
    {
        var fake = new InMemoryDigestFiles();
        return (fake, (name, content) =>
        {
            fake.Put("/d/" + name, content.Length, A.T0);
            return "/d/" + name;
        }
        );
    }
}
