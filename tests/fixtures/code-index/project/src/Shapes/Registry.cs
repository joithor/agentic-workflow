namespace Fixture.Shapes;

public sealed class Registry<T> where T : IShape
{
    private readonly List<T> _items;

    public Registry()
    {
        _items = new List<T>();
    }

    public int Register(T item)
    {
        _items.Add(item);
        return _items.Count;
    }

    public double TotalArea()
    {
        double total = 0;
        foreach (var item in _items)
        {
            total += item.Area();
        }

        return total;
    }

    public IReadOnlyList<TOut> Select<TOut>(Func<T, TOut> projection) => _items.MapAll(projection);
}
