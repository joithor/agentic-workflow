using Aw.Architecture.Tests.Fixtures.CrossB.Core;

namespace Aw.Architecture.Tests.Fixtures.CrossB.Infrastructure;

internal sealed class LedgerStore
{
    public static LedgerId Open(string value) => new(value);
}
