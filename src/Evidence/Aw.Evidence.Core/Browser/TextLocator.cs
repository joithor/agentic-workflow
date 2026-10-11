namespace Aw.Evidence.Core.Browser;

/// <summary>The first visible element showing <paramref name="Text"/>; waiting for it hidden means no visible match remains.</summary>
/// <param name="Text">The text.</param>
public sealed record TextLocator(string Text) : Locator;
