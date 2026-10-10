// Writes each corpus string (one JSON string literal per input line, so the corpus file itself is plain JSON)
// as a JSON string with the encoder named in args[0]: default | relaxed. One output line per input line.
using System.Text.Encodings.Web;
using System.Text.Json;

var encoder = args[0] switch
{
    "default" => JavaScriptEncoder.Default,
    "relaxed" => JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    _ => throw new ArgumentException("encoder: default | relaxed"),
};
var options = new JsonWriterOptions { Encoder = encoder };
foreach (var line in File.ReadLines(args[1]))
{
    string value;
    try
    {
        using var doc = JsonDocument.Parse(line);
        value = doc.RootElement.GetString()!;
    }
    catch (InvalidOperationException e)
    {
        // STJ's reader rejects some strings Node writes (a lone surrogate): record it, then unescape by hand
        // so the writer can still be compared.
        Console.Error.WriteLine($"reader rejects {line}: {e.Message}");
        value = System.Text.RegularExpressions.Regex.Unescape(line[1..^1]);
    }
    using var ms = new MemoryStream();
    try
    {
        using (var w = new Utf8JsonWriter(ms, options)) w.WriteStringValue(value);
        Console.Out.Write(System.Text.Encoding.UTF8.GetString(ms.ToArray()) + "\n");
    }
    catch (ArgumentException e)
    {
        Console.Out.Write($"<writer throws {e.GetType().Name}>\n");
    }
}
