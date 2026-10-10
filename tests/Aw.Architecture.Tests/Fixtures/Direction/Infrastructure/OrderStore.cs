using Aw.Architecture.Tests.Fixtures.Direction.Features.Ship;

namespace Aw.Architecture.Tests.Fixtures.Direction.Infrastructure;

internal sealed class OrderStore
{
    public static long Shipped => ShipService.When;
}
