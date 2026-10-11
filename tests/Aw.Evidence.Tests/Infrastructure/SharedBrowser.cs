namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The browser tests share one Chromium and one fixture site.</summary>
[CollectionDefinition(Name)]
public sealed class SharedBrowser : ICollectionFixture<BrowserFixture>
{
    public const string Name = "browser";
}
