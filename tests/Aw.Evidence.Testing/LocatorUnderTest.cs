using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>A locator arranged by an <see cref="ExecutableLocatorContract"/> subclass over the contract's fixed layout, and the layout's root.</summary>
/// <param name="Locator">The locator under test.</param>
/// <param name="Root">The directory the layout sits in (a real path with no symlink in it).</param>
public sealed record LocatorUnderTest(IExecutableLocator Locator, string Root);
