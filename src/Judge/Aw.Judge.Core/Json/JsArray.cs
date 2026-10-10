namespace Aw.Judge.Core.Json;

/// <summary>A JSON array.</summary>
public sealed class JsArray : JsValue
{
    private readonly List<JsValue> _items;

    /// <summary>An array of <paramref name="items"/>, in order.</summary>
    /// <param name="items">The elements.</param>
    public JsArray(IEnumerable<JsValue> items)
    {
        _items = [.. items];
    }

    internal JsArray()
    {
        _items = [];
    }

    /// <summary>The elements, in order.</summary>
    public IReadOnlyList<JsValue> Items => _items;

    internal void Add(JsValue item) => _items.Add(item);
}
