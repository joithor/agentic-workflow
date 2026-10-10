using System.Globalization;
using System.Text;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The message text of a log call. When a structured field has a secret's name (password, token, …) its value is
/// withheld from the message too: the message is rendered again from its template with that hole replaced. Other
/// holes take the value's invariant text, so such a message can differ from the formatter's only in formatting.
/// </summary>
internal static class MessageText
{
    public const string TemplateKey = "{OriginalFormat}";

    public static string Render(string formatted, IReadOnlyList<KeyValuePair<string, object?>> fields, Func<string, bool> isSecretName)
    {
        if (fields.FirstOrDefault(f => f.Key == TemplateKey).Value is not string template
            || !fields.Any(f => f.Key != TemplateKey && isSecretName(f.Key)))
        {
            return formatted;
        }

        return RenderTemplate(template, fields, isSecretName);
    }

    private static string RenderTemplate(string template, IReadOnlyList<KeyValuePair<string, object?>> fields, Func<string, bool> isSecretName)
    {
        var text = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if ((c is '{' or '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                _ = text.Append(c);
                i += 2;
                continue;
            }

            var close = c == '{' ? template.IndexOf('}', i + 1) : -1;
            if (close < 0)
            {
                _ = text.Append(c);
                i++;
                continue;
            }

            _ = text.Append(Hole(template[(i + 1)..close], fields, isSecretName));
            i = close + 1;
        }

        return text.ToString();
    }

    private static string Hole(string hole, IReadOnlyList<KeyValuePair<string, object?>> fields, Func<string, bool> isSecretName)
    {
        var end = hole.AsSpan().IndexOfAny(',', ':');
        var name = end < 0 ? hole : hole[..end];
        var format = end >= 0 && hole[end] == ':' ? hole[(end + 1)..] : null;
        if (isSecretName(name))
        {
            return Aw.Redaction.ScrubResult.SecretFieldText;
        }

        var value = fields.FirstOrDefault(f => f.Key == name).Value;
        return value is IFormattable formattable && format is not null
            ? formattable.ToString(format, CultureInfo.InvariantCulture)
            : Scrubber.Text(value);
    }
}
