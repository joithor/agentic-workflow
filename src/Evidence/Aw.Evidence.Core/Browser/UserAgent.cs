namespace Aw.Evidence.Core.Browser;

/// <summary>
/// The user agent a session presents. Headless Chromium says 'HeadlessChrome', which apps that gate on browser
/// support refuse before any step runs, so the same build presents as ordinary desktop Chrome.
/// </summary>
public static class UserAgent
{
    /// <summary>Desktop Chrome at <paramref name="browserVersion"/>, unless a non-blank override is given.</summary>
    /// <param name="browserVersion">The browser build.</param>
    /// <param name="overrideValue">UI_EVIDENCE_USER_AGENT.</param>
    /// <returns>The user agent.</returns>
    public static string For(string browserVersion, string? overrideValue) =>
        string.IsNullOrWhiteSpace(overrideValue)
            ? $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{browserVersion} Safari/537.36"
            : overrideValue;
}
