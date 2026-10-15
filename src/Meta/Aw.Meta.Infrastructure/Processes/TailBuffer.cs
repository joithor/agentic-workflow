using System.Text;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>Keeps the last characters written to it, so a command's output is bounded however long it runs.</summary>
/// <param name="capacity">The most characters kept.</param>
internal sealed class TailBuffer(int capacity)
{
    private readonly StringBuilder _text = new();

    internal void Append(ReadOnlySpan<char> chunk)
    {
        _ = _text.Append(chunk);
        if (_text.Length > capacity)
        {
            _ = _text.Remove(0, _text.Length - capacity);
        }
    }

    public override string ToString() => _text.ToString();
}
