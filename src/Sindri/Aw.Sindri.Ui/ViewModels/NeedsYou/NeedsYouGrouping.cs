namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Which group of the inbox a row belongs to, by its id prefix and kind (needs-you spec §4).</summary>
internal static class NeedsYouGrouping
{
    /// <summary>The group of a row.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>Its kind.</returns>
    internal static NeedsYouGroupKind KindOf(NeedsYouEntry entry) => throw new NotSupportedException();

    /// <summary>Splits rows into the inbox's groups: the canonical order, empty groups left out, each group's rows in the order given.</summary>
    /// <param name="rows">The rows, in the API's order.</param>
    /// <param name="entry">The served entry of a row.</param>
    /// <typeparam name="T">What a row is.</typeparam>
    /// <returns>The groups.</returns>
    internal static IReadOnlyList<NeedsYouGroup<T>> Split<T>(IEnumerable<T> rows, Func<T, NeedsYouEntry> entry) => throw new NotSupportedException();
}
