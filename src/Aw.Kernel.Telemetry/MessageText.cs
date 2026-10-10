using System.Globalization;
using System.Text;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The message text of a log call. When a structured field has a secret's name (password, token, …), or holds a
/// dictionary with a secret-named entry, its value is withheld from the message too: the message is rendered again from
/// its template, the way the log formatter does (alignment and format applied; null is "(null)"; a list is its elements
/// joined with ", "), with that value withheld. Each hole takes the field of its own name (the k-th hole named N takes
/// the k-th field named N, ignoring a '@' or '$' prefix), never a field by position: a source-generated
/// [LoggerMessage] state lists its fields in parameter order, which can differ from the template's.
/// </summary>
internal static class MessageText
{
    public const string TemplateKey = "{OriginalFormat}";
    private const string NullText = "(null)";

    public static string Render(string formatted, IReadOnlyList<KeyValuePair<string, object?>> fields, Scrubber scrubber)
    {
        if (fields.FirstOrDefault(f => f.Key == TemplateKey).Value is not string template
            || !fields.Any(f => f.Key != TemplateKey && (scrubber.IsSecretName(f.Key) || scrubber.HasSecretEntries(f.Value))))
        {
            return formatted;
        }

        var values = fields.Where(f => f.Key != TemplateKey).ToList();
        return RenderTemplate(template, values, scrubber);
    }

    private static string RenderTemplate(string template, List<KeyValuePair<string, object?>> values, Scrubber scrubber)
    {
        var text = new StringBuilder(template.Length);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            var close = c == '{' ? template.IndexOf('}', i + 1) : -1;
            if ((c is '{' or '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                _ = text.Append(c);
                i += 2;
            }
            else if (close > i)
            {
                _ = text.Append(Hole(template[(i + 1)..close], values, seen, scrubber));
                i = close + 1;
            }
            else
            {
                _ = text.Append(c);
                i++;
            }
        }

        return text.ToString();
    }

    private static string Hole(string hole, List<KeyValuePair<string, object?>> values, Dictionary<string, int> seen, Scrubber scrubber)
    {
        var end = hole.AsSpan().IndexOfAny(',', ':');
        var name = Bare(end < 0 ? hole : hole[..end]);
        if (scrubber.IsSecretName(name))
        {
            return Redaction.ScrubResult.SecretFieldText;
        }

        var occurrence = seen[name] = seen.GetValueOrDefault(name) + 1;
        var named = values.Where(f => Bare(f.Key) == name).ToList();
        var value = named.Count == 0 ? null : named[Math.Min(occurrence, named.Count) - 1].Value;
        var text = value switch
        {
            null => NullText,
            string or System.Collections.IEnumerable => scrubber.Render(value),
            var other => other,
        };
        return end < 0 ? Convert.ToString(text, CultureInfo.InvariantCulture) ?? NullText
            : string.Format(CultureInfo.InvariantCulture, "{0" + hole[end..] + "}", text);
    }

    private static string Bare(string name) => name.TrimStart('@', '$');
}
