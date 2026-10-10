using Aw.Architecture.Tests.Fixtures.Locator.Features.Run;

namespace Aw.Architecture.Tests.Fixtures.Locator.Features;

public static class LocatorFeaturesServiceCollectionExtensions
{
    internal static Func<IServiceProvider, RunService> Factory { get; } = static sp => new RunService(sp);
}
