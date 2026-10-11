using System.Globalization;
using Aw.Evidence.Core.Browser;
using Microsoft.Playwright;
using Locator = Aw.Evidence.Core.Browser.Locator;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>Maps Core's locators onto Playwright: test id, first visible text match, first CSS match, interactive element by index.</summary>
internal static class PlaywrightLocators
{
    /// <summary>The Playwright locator for <paramref name="target"/>.</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <returns>The locator.</returns>
    internal static ILocator Resolve(IPage page, Locator target) => target switch
    {
        TestIdLocator id => page.GetByTestId(id.TestId.Value),
        TextLocator text => page.GetByText(text.Text).Locator("visible=true").First,
        CssLocator css => page.Locator(css.Css).First,
        CandidateLocator candidate => page.Locator(PageScripts.Interactive).Nth(candidate.Index),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "unknown locator"),
    };

    /// <summary>For a candidate, checks the element still carries the test id it had when enumerated (the DOM didn't shift).</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <returns>The locator to act on.</returns>
    /// <exception cref="PlaywrightException">The element moved or is missing.</exception>
    internal static async Task<ILocator> ResolveCheckedAsync(IPage page, Locator target, TimeSpan timeout)
    {
        var locator = Resolve(page, target);
        if (target is CandidateLocator candidate)
        {
            var testId = await locator.GetAttributeAsync("data-testid", new() { Timeout = Ms(timeout) }).ConfigureAwait(false);
            if (!string.Equals(testId, candidate.ExpectedTestId?.Value, StringComparison.Ordinal))
            {
                throw new PlaywrightException(string.Create(CultureInfo.InvariantCulture, $"candidate {candidate.Index} is no longer the element it was (the DOM shifted)"));
            }
        }

        return locator;
    }

    /// <summary>A timeout in Playwright's milliseconds.</summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>Milliseconds.</returns>
    internal static float Ms(TimeSpan timeout) => (float)timeout.TotalMilliseconds;
}
