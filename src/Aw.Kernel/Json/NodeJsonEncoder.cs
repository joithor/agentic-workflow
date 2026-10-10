using System.Text;
using System.Text.Encodings.Web;

namespace Aw.Kernel.Json;

/// <summary>
/// The encoder System.Text.Json uses for property names and dictionary keys, matching 'JSON.stringify':
/// only quotes, backslashes and control characters are escaped. String values go through
/// <see cref="NodeStringConverter"/> instead, which also reproduces Node's lone-surrogate escapes.
/// </summary>
internal sealed class NodeJsonEncoder : JavaScriptEncoder
{
    private NodeJsonEncoder()
    {
    }

    public static NodeJsonEncoder Instance { get; } = new();

    public override int MaxOutputCharactersPerInputCharacter => 6;

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        var span = new ReadOnlySpan<char>(text, textLength);
        for (var i = 0; i < span.Length; i++)
        {
            if (WillEncode(span[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        var destination = new Span<char>(buffer, bufferLength);
        var escaped = NodeJsonEscaping.EscapeQuoteOrControl(unicodeScalar);
        if (escaped is null)
        {
            return new Rune(unicodeScalar).TryEncodeToUtf16(destination, out numberOfCharactersWritten);
        }

        numberOfCharactersWritten = 0;
        if (!escaped.AsSpan().TryCopyTo(destination))
        {
            return false;
        }

        numberOfCharactersWritten = escaped.Length;
        return true;
    }

    public override bool WillEncode(int unicodeScalar) => NodeJsonEscaping.EscapeQuoteOrControl(unicodeScalar) is not null;
}
