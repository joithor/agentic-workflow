using Aw.Architecture.Tests.Fixtures.SmallGood.App.Infrastructure;
using Aw.Architecture.Tests.Fixtures.SmallGood.Core;

namespace Aw.Architecture.Tests.Fixtures.SmallGood.App;

internal static class Composition
{
    public static Job Seed(string name) => JobStore.Load(name);
}
