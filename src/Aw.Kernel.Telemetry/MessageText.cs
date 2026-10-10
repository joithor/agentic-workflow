using System.Collections;
using System.Globalization;
using System.Text;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The message text of a log call. When a structured field has a secret's name (password, token, …) its value is
/// withheld from the message too: the message is rendered again from its template, the way the log formatter does
/// (holes take values by position, with their alignment and format; null is "(null)"; a list is its elements joined
/// with ", "), with that hole replaced.
/// </summary>
internal static class MessageText
{
    public const string TemplateKey = "{OriginalFormat}";
    private const string NullText = "(null)";

    public static string Render(string formatted, IReadOnlyList<KeyValuePair<string, object?>> fields, Func<string, bool> isSecretName)
    {
        if (fields.FirstOrDefault(f => f.Key == TemplateKey).Value is not string template
            || !fields.Any(f => f.Key != TemplateKey && isSecretName(f.Key)))
        {
            return formatted;
        }

        var values = fields.Where(f => f.Key != TemplateKey).ToList();
        return RenderTemplate(template, values, isSecretName);
    }

    private static string RenderTemplate(string template, List<KeyValuePair<string, object?>> values, Func<string, bool> isSecretName)
    {
        var text = new StringBuilder(template.Length);
        var hole = 0;
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
                _ = text.Append(Hole(template[(i + 1)..close], hole < values.Count ? values[hole] : default, isSecretName));
                hole++;
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

    private static string Hole(string hole, KeyValuePair<string, object?> field, Func<string, bool> isSecretName)
    {
        var end = hole.AsSpan().IndexOfAny(',', ':');
        var name = end < 0 ? hole : hole[..end];
        if (isSecretName(name))
        {
            return Redaction.ScrubResult.SecretFieldText;
        }

        var value = field.Value switch
        {
            null => NullText,
            string s => s,
            IEnumerable items => string.Join(", ", items.Cast<object?>().Select(v => v ?? NullText)),
            var other => other,
        };
        return end < 0 ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? NullText
            : string.Format(CultureInfo.InvariantCulture, "{0" + hole[end..] + "}", value);
    }
}
