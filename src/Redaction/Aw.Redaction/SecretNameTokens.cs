namespace Aw.Redaction;

/// <summary>
/// The token half of the secret-name rule. A name is split into tokens at every character that is not a letter or digit
/// (_, -, ., a space) and at camelCase humps (apiTokens is api, tokens; APIKey is api, key),
/// lower-cased. It holds a secret when a token equals a secret word (password, passwd, secret, token, apikey, accesskey,
/// privatekey, secretkey, each also in the plural) or when two neighbouring tokens spell one (api + key, access + key,
/// private + key, secret + key). Whole-token equality is the rule, so tokenizer, secretary and passwordless are ordinary.
/// The word token (a count in LLM and telemetry names) is ordinary when a count word sits beside it (max_tokens,
/// input_tokens, token_count, tokens_used, totalTokens).
/// </summary>
internal static class SecretNameTokens
{
    private static readonly HashSet<string> _words = new(StringComparer.Ordinal)
    {
        "password", "passwords", "passwd", "secret", "secrets", "apikey", "apikeys", "accesskey", "accesskeys",
        "privatekey", "privatekeys", "secretkey", "secretkeys",
    };

    private static readonly HashSet<string> _tokenWords = new(StringComparer.Ordinal) { "token", "tokens" };

    private static readonly HashSet<string> _keyPrefixes = new(StringComparer.Ordinal) { "api", "access", "private", "secret" };

    private static readonly HashSet<string> _keyWords = new(StringComparer.Ordinal) { "key", "keys" };

    private static readonly HashSet<string> _countWords = new(StringComparer.Ordinal)
    {
        "max", "min", "count", "counts", "total", "num", "number", "limit", "limits", "usage", "input", "output", "prompt",
        "completion", "cached", "reasoning", "budget", "remaining", "used", "size", "len", "length", "per",
    };

    /// <summary>True when a token of <paramref name="name"/> (or two neighbours) is a secret word.</summary>
    internal static bool Holds(string name)
    {
        var tokens = Split(name);
        var counted = tokens.Exists(_countWords.Contains);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (_words.Contains(tokens[i])
                || (_tokenWords.Contains(tokens[i]) && !counted)
                || (i > 0 && _keyPrefixes.Contains(tokens[i - 1]) && _keyWords.Contains(tokens[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Split(string name)
    {
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= name.Length; i++)
        {
            var c = i < name.Length ? name[i] : ' ';
            var alnum = char.IsAsciiLetterOrDigit(c);
            if (start >= 0 && (!alnum || StartsHump(name, i)))
            {
                tokens.Add(name[start..i].ToLowerInvariant());
                start = -1;
            }

            if (alnum && start < 0)
            {
                start = i;
            }
        }

        return tokens;
    }

    // A hump starts at an upper-case letter after a lower-case letter or digit (apiKey), or before the last upper-case letter of a run that is followed by lower case (APIKey).
    private static bool StartsHump(string name, int i)
    {
        if (i == 0 || i >= name.Length || !char.IsAsciiLetterUpper(name[i]))
        {
            return false;
        }

        var before = name[i - 1];
        return char.IsAsciiLetterLower(before) || char.IsAsciiDigit(before)
            || (char.IsAsciiLetterUpper(before) && i + 1 < name.Length && char.IsAsciiLetterLower(name[i + 1]));
    }
}
