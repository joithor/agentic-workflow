using Aw.Kernel.Testing;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemorySizesSourceContractTests : SizesSourceContract
{
    protected override ISizesSource Create(string path, byte[] bytes) => new InMemorySizesSource().With(path, bytes);
}
