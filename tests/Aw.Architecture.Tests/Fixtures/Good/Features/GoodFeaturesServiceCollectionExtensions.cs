using Aw.Architecture.Tests.Fixtures.Good.Core;
using Aw.Architecture.Tests.Fixtures.Good.Features.StartItem;

namespace Aw.Architecture.Tests.Fixtures.Good.Features;

// The decorator factory lambda /new-slice registers: resolve the inner service, construct, no logic.
public static class GoodFeaturesServiceCollectionExtensions
{
    internal static Func<IServiceProvider, StartItemService> StartItem { get; } =
        static sp => new StartItemService((IItemStore)(sp.GetService(typeof(IItemStore)) ?? throw new InvalidOperationException("no store")));
}
