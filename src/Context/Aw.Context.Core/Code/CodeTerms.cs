using System.Text;

namespace Aw.Context.Core.Code;

/// <summary>The search terms for a data thing (S-CL): a name in four forms, case-insensitive, at least <see cref="CodeRules.MinTermLength"/> characters.</summary>
public static class CodeTerms
{
    /// <summary>The name part of a node key ('thing:shared-link' gives 'shared-link').</summary>
    /// <param name="key">The node key.</param>
    /// <returns>The part after the first ':', or the key itself.</returns>
    public static string KeyName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? key : key[(colon + 1)..];
    }

    /// <summary>The searchable forms of some names (a key name, a label, aliases): words split on '-_./' and space and on camel boundaries, written as kebab, snake, joined and spaced, lower-cased.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The distinct forms of at least <see cref="CodeRules.MinTermLength"/> characters, in order; empty when none qualifies (the thing is not searchable).</returns>
    public static IReadOnlyList<string> Forms(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var forms = new List<string>();
        foreach (var name in names)
        {
            var words = Words(name);
            if (words.Count == 0)
            {
                continue;
            }

            foreach (var form in new[] { string.Join('-', words), string.Join('_', words), string.Concat(words), string.Join(' ', words) })
            {
                if (form.Length >= CodeRules.MinTermLength && form.Length <= CodeRules.MaxTermLength && !forms.Contains(form, StringComparer.Ordinal))
                {
                    forms.Add(form);
                }
            }
        }

        return forms;
    }

    private static List<string> Words(string name)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c))
            {
                Flush(words, word);
                continue;
            }

            if (word.Length > 0 && StartsWord(name, i))
            {
                Flush(words, word);
            }

            _ = word.Append(char.ToLowerInvariant(c));
        }

        Flush(words, word);
        return words;
    }

    // A new word starts at an upper-case letter after a lower-case letter or digit ('sharedLink'), and at the last upper-case
    // letter of a run that is followed by a lower-case one ('XMLParser' is 'xml' and 'parser').
    private static bool StartsWord(string name, int i)
    {
        if (!char.IsUpper(name[i]))
        {
            return false;
        }

        var previous = name[i - 1];
        return char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]));
    }

    private static void Flush(List<string> words, StringBuilder word)
    {
        if (word.Length > 0)
        {
            words.Add(word.ToString());
            _ = word.Clear();
        }
    }
}
