namespace Aw.Evidence.Core.Browser;

/// <summary>The first element matching <paramref name="Css"/>.</summary>
/// <param name="Css">The CSS selector.</param>
public sealed record CssLocator(string Css) : Locator;
