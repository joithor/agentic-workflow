using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Browser;

/// <summary>The element with <paramref name="TestId"/>.</summary>
/// <param name="TestId">The data-testid.</param>
public sealed record TestIdLocator(TestId TestId) : Locator;
