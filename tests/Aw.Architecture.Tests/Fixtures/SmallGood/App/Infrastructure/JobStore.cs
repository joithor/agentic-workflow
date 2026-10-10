using Aw.Architecture.Tests.Fixtures.SmallGood.Core;

namespace Aw.Architecture.Tests.Fixtures.SmallGood.App.Infrastructure;

internal sealed class JobStore
{
    public static Job Load(string name) => new(name);
}
