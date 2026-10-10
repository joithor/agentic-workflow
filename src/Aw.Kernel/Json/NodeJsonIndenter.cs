using System.Buffers;

namespace Aw.Kernel.Json;

/// <summary>
/// Re-indents compact JSON exactly as 'JSON.stringify(value, null, 2)' lays it out: two spaces per level,
/// '\n' line breaks, '": "' after a key, and empty arrays and objects kept as '[]' and '{}'.
/// System.Text.Json's own indentation can't be used: it doesn't indent the raw values the Node converters write.
/// </summary>
internal sealed class NodeJsonIndenter
{
    internal const int IndentSize = 2;
    private readonly ArrayBufferWriter<byte> _output;
    private int _depth;

    private NodeJsonIndenter(int capacity)
    {
        _output = new ArrayBufferWriter<byte>(capacity);
    }

    public static byte[] Indent(ReadOnlySpan<byte> compact)
    {
        var indenter = new NodeJsonIndenter(compact.Length * 2);
        var i = 0;
        while (i < compact.Length)
        {
            i = compact[i] == (byte)'"' ? indenter.CopyString(compact, i) : indenter.Structure(compact, i);
        }

        return indenter._output.WrittenSpan.ToArray();
    }

    /// <summary>Copies a string token unchanged and returns the index after its closing quote.</summary>
    private int CopyString(ReadOnlySpan<byte> json, int start)
    {
        var i = start + 1;
        while (json[i] != (byte)'"')
        {
            i += json[i] == (byte)'\\' ? 2 : 1;
        }

        Write(json[start..(i + 1)]);
        return i + 1;
    }

    /// <summary>Writes one byte outside a string, with the layout around it, and returns the next index.</summary>
    private int Structure(ReadOnlySpan<byte> json, int i)
    {
        var b = json[i];
        switch (b)
        {
            case (byte)'{' or (byte)'[' when IsEmpty(json, i):
                Write(json.Slice(i, 2));
                return i + 2;
            case (byte)'{' or (byte)'[':
                Write(b);
                _depth++;
                NewLine();
                break;
            case (byte)'}' or (byte)']':
                _depth--;
                NewLine();
                Write(b);
                break;
            case (byte)',':
                Write(b);
                NewLine();
                break;
            case (byte)':':
                Write(": "u8);
                break;
            default:
                Write(b);
                break;
        }

        return i + 1;
    }

    private static bool IsEmpty(ReadOnlySpan<byte> json, int open) =>
        open + 1 < json.Length && json[open + 1] == (json[open] == (byte)'{' ? (byte)'}' : (byte)']');

    private void NewLine()
    {
        Write((byte)'\n');
        var span = _output.GetSpan(_depth * IndentSize)[..(_depth * IndentSize)];
        span.Fill((byte)' ');
        _output.Advance(span.Length);
    }

    private void Write(byte b) => Write([b]);

    private void Write(ReadOnlySpan<byte> bytes) => _output.Write(bytes);
}
