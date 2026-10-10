using Aw.Architecture.Tests.Fixtures.CrossB.Core;
using Aw.Architecture.Tests.Fixtures.CrossB.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.CrossA.Features.Pay;

internal sealed class PayService
{
    public static LedgerId Pay(string value) => LedgerStore.Open(value);
}
