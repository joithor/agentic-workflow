using Aw.Architecture.Tests.Fixtures.Direction.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.Direction.Core;

internal sealed class Order
{
    public static string Id(string id) => new OrderRow(id).Id;
}
