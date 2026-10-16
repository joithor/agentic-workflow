namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The rows that fit above Home's fold at 1280 × 800, in one place (screens §4.1): the Needs-you list's cap and the strip's share of what is
/// left, at text scale 1.0 and from <see cref="LargeTextScale"/> up. The strip's budget is the Needs-you cap plus its share, so a change to
/// the Needs-you cap moves the fold with it and cannot silently break it.
/// </summary>
internal static class HomeFold
{
    /// <summary>The text scale from which both lists show fewer rows.</summary>
    internal const double LargeTextScale = 1.3;

    /// <summary>The most Needs-you rows Home shows before its overflow control.</summary>
    internal const int NeedsYouCap = 5;

    /// <summary>The most Needs-you rows at <see cref="LargeTextScale"/> and over.</summary>
    internal const int NeedsYouLargeCap = 4;

    /// <summary>The most strip rows (screens §4.1).</summary>
    internal const int StripCap = 5;

    /// <summary>The most strip rows at <see cref="LargeTextScale"/> and over.</summary>
    internal const int StripLargeCap = 2;

    /// <summary>The strip's share of the fold at scale 1.0: 3 rows.</summary>
    internal const int StripShare = 3;

    /// <summary>The strip's share of the fold at <see cref="LargeTextScale"/> and over: the large Needs-you cap plus <see cref="StripLargeCap"/>.</summary>
    internal const int StripLargeShare = StripLargeCap;

    /// <summary>Rows above the fold at scale 1.0 (Needs you plus strip): 8.</summary>
    internal const int Budget = NeedsYouCap + StripShare;

    /// <summary>Rows above the fold at <see cref="LargeTextScale"/> and over: 6.</summary>
    internal const int LargeBudget = NeedsYouLargeCap + StripLargeShare;

    /// <summary>True when the text scale is large enough to drop the caps.</summary>
    /// <param name="scale">The text scale.</param>
    /// <returns>True from <see cref="LargeTextScale"/> up.</returns>
    internal static bool IsLarge(double scale) => scale >= LargeTextScale;
}
