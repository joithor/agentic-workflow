using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Plain scalars by the YAML 1.2 core schema (spec §3.1): null ('', '~', 'null'), booleans ('true', 'false' in three
/// cases), integers (decimal, '0o' octal, '0x' hex) and floats are typed; everything else, and every quoted or block
/// scalar, is text. So 'on', 'yes' and 'no' stay text. '.inf' and '.nan' are numbers JSON can't hold (RCP-SCHEMA-002).
/// </summary>
internal static partial class YamlScalars
{
    public static JsonNode? Resolve(Scalar scalar, string path, ReaderFindings findings)
    {
        var text = scalar.Value;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }

        return text switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(value: true),
            "false" or "False" or "FALSE" => JsonValue.Create(value: false),
            _ => Number(text, path, findings),
        };
    }

    private static JsonNode? Number(string text, string path, ReaderFindings findings)
    {
        if (Integer.IsMatch(text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
        {
            return JsonValue.Create(whole);
        }

        if (text.StartsWith("0o", StringComparison.Ordinal) && Octal.IsMatch(text))
        {
            return Based(text[2..], 8, path, findings);
        }

        if (text.StartsWith("0x", StringComparison.Ordinal) && Hex.IsMatch(text))
        {
            return Based(text[2..], 16, path, findings);
        }

        return Real(text, path, findings);
    }

    private static JsonNode? Real(string text, string path, ReaderFindings findings)
    {
        if (!Integer.IsMatch(text) && !Float.IsMatch(text) && !Special.IsMatch(text))
        {
            return JsonValue.Create(text);
        }

        var value = Special.IsMatch(text) ? double.NaN : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        return double.IsFinite(value) ? JsonValue.Create(value) : Unrepresentable(text, path, findings);
    }

    private static JsonNode? Based(string digits, int radix, string path, ReaderFindings findings)
    {
        try
        {
            return JsonValue.Create(Convert.ToInt64(digits, radix));
        }
        catch (OverflowException)
        {
            return Unrepresentable(digits, path, findings);
        }
    }

    private static JsonNode? Unrepresentable(string text, string path, ReaderFindings findings)
    {
        findings.Add("RCP-SCHEMA-002", path, $"'{text}' is a number JSON can't hold", "write a finite number, or quote it to make it text");
        findings.Refuse(path);
        return null;
    }

    [GeneratedRegex("^[-+]?[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Integer { get; }

    [GeneratedRegex("^0o[0-7]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Octal { get; }

    [GeneratedRegex("^0x[0-9a-fA-F]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Hex { get; }

    [GeneratedRegex("^[-+]?(\\.[0-9]+|[0-9]+(\\.[0-9]*)?)([eE][-+]?[0-9]+)?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Float { get; }

    [GeneratedRegex("^([-+]?\\.(inf|Inf|INF)|\\.nan|\\.NaN|\\.NAN)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Special { get; }
}
