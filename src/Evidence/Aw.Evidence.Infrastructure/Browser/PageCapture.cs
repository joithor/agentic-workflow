using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Lint;
using Microsoft.Playwright;
using ViewportSize = Aw.Evidence.Core.Geometry.ViewportSize;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>Reads the page into Core's shapes: visible candidates, the lint snapshot, element boxes.</summary>
internal static class PageCapture
{
    /// <summary>The currently visible interactive elements, in DOM order.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The candidates.</returns>
    internal static async Task<IReadOnlyList<Candidate>> VisibleCandidatesAsync(IPage page)
    {
        var rows = await page.EvaluateAsync<CandidateRow[]>(PageScripts.VisibleCandidates, PageScripts.Interactive).ConfigureAwait(false);
        return [.. (rows ?? []).Select(static r => new Candidate(r.Index, r.Role, r.AccessibleName, TestIdOrNull(r.TestId), r.Text))];
    }

    /// <summary>The elements and styles the lint rules read.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The snapshot.</returns>
    internal static async Task<PageSnapshot> LintSnapshotAsync(IPage page)
    {
        var rows = await page.EvaluateAsync<LintRow[]>(PageScripts.LintSnapshot).ConfigureAwait(false);
        var size = page.ViewportSize;
        return new(
            [.. (rows ?? []).Select(static r => new PageElement(
                r.Selector, Round(r.X, r.Y, r.W, r.H), r.Text, new ElementStyle(r.TextOverflow, r.Overflow, r.Color), r.HasHoverState, r.HasFocusState))],
            new ViewportSize(size?.Width ?? 0, size?.Height ?? 0));
    }

    /// <summary>An element's bounding box, two decimals.</summary>
    /// <param name="locator">The element.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <returns>The box.</returns>
    /// <exception cref="PlaywrightException">The element isn't there or has no box.</exception>
    internal static async Task<Box> BoundingBoxAsync(ILocator locator, TimeSpan timeout)
    {
        var box = await locator.BoundingBoxAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false)
            ?? throw new PlaywrightException("the element has no bounding box (not rendered)");
        return Round(box.X, box.Y, box.Width, box.Height);
    }

    private static Box Round(double x, double y, double w, double h) =>
        new(Two(x), Two(y), Two(w), Two(h));

    private static double Two(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static TestId? TestIdOrNull(string? value) =>
        value is not null && TestId.TryFrom(value, out var id) ? id : null;
}
