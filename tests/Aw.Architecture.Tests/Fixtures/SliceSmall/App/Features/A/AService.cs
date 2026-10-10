using Aw.Architecture.Tests.Fixtures.SliceSmall.App.Features.B;

namespace Aw.Architecture.Tests.Fixtures.SliceSmall.App.Features.A;

internal sealed class AService
{
    public static int Size => new BRequest(1).Size;
}
