namespace Fixture.Shapes;

public static class ShapeExtensions
{
    public static string ToSummary(this IShape shape) => shape.Describe() + " area " + Labeler.Format(shape.Area());

    public static IReadOnlyList<TOut> MapAll<TIn, TOut>(this IEnumerable<TIn> items, Func<TIn, TOut> map)
    {
        var result = new List<TOut>();
        foreach (var item in items)
        {
            result.Add(map(item));
        }

        return result;
    }

    public static string Banner(IShape shape) => shape.ToSummary().ToUpperInvariant() + ToSummary(shape);
}
