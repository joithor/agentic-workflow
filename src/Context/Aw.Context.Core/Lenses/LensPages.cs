namespace Aw.Context.Core.Lenses;

/// <summary>Takes one page of a lenses answer: the same offset and limit from every list that carries the bulk of the payload.</summary>
public static class LensPages
{
    /// <summary>The page of <paramref name="view"/> at <paramref name="offset"/>, <paramref name="limit"/> items long.</summary>
    /// <param name="view">The whole answer.</param>
    /// <param name="limit">How many items to keep from each list.</param>
    /// <param name="offset">How many items to skip.</param>
    /// <returns>The page. Counts and coverage are the full answer's.</returns>
    public static LensesView Window(LensesView view, int limit, int offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        var (bands, edges, more) = Cut(view.Bands, view.Edges, limit, offset);
        return view with { Bands = bands, Edges = edges, Page = new LensWindow(offset, limit, more) };
    }

    /// <summary>The page of <paramref name="view"/> at <paramref name="offset"/>, <paramref name="limit"/> items long.</summary>
    /// <param name="view">The whole answer.</param>
    /// <param name="limit">How many items to keep from each list.</param>
    /// <param name="offset">How many items to skip.</param>
    /// <returns>The page. Counts and coverage are the full answer's.</returns>
    public static LensesSetView Window(LensesSetView view, int limit, int offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        var (bands, edges, more) = Cut(view.Bands, view.Edges, limit, offset);
        return view with { Bands = bands, Edges = edges, Page = new LensWindow(offset, limit, more) };
    }

    private static (LensBands Bands, IReadOnlyList<LensEdge> Edges, bool More) Cut(
        LensBands bands, IReadOnlyList<LensEdge> edges, int limit, int offset)
    {
        var more = false;
        var product = bands.Product;
        var intent = bands.Intent;
        var code = bands.Code;
        return (
            bands with
            {
                Product = product with
                {
                    Own = Take(product.Own, limit, offset, ref more),
                    Neighbors = Take(product.Neighbors, limit, offset, ref more),
                    Affects = Take(product.Affects, limit, offset, ref more),
                    Edges = Take(product.Edges, limit, offset, ref more),
                },
                Intent = intent with
                {
                    Records = Take(intent.Records, limit, offset, ref more),
                    Decisions = Take(intent.Decisions, limit, offset, ref more),
                },
                Code = code with { Files = Take(code.Files, limit, offset, ref more) },
            },
            Take(edges, limit, offset, ref more),
            more);
    }

    private static T[] Take<T>(IReadOnlyList<T> items, int limit, int offset, ref bool more)
    {
        if (items.Count > (long)offset + limit)
        {
            more = true;
        }

        if (offset >= items.Count)
        {
            return [];
        }

        var count = Math.Min(limit, items.Count - offset);
        var kept = new T[count];
        for (var index = 0; index < count; index++)
        {
            kept[index] = items[offset + index];
        }

        return kept;
    }
}
