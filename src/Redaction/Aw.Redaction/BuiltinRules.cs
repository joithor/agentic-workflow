using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>
/// The one pattern set (spec §4c): the union of judge's redactSecrets and sindri's scrub patterns, with the gaps the
/// A3 differential found closed. Order matters: when two rules overlap at the same start, the earlier one names the
/// hit. Patterns use explicit ASCII classes and lookarounds instead of \b, \s and \d (which are Unicode-aware in
/// .NET). Deliberately absent: judge's bare 40-hex rule, which erases git SHAs and digests and matches no secret
/// format.
/// </summary>
internal static class BuiltinRules
{
    // A token starts where no word character precedes it (\b without Unicode word characters).
    private const string Start = "(?<![A-Za-z0-9_])";
    private const string End = "(?![A-Za-z0-9_])";

    // The value of a KEY=value pair: 20+ token characters with a digit in the first 256 (so identifiers and words
    // don't count; bounded, so a long run stays linear).
    private const string Value = "(?=[A-Za-z0-9+/=_-]{0,255}?[0-9])(?<v>[A-Za-z0-9+/=_-]{20,})";

    private const string SecretWord = "(?:API[_-]?KEY|SECRET(?:[_-]?KEY)?|TOKEN|PASSWORD|PRIVATE[_-]?KEY|ACCESS[_-]?KEY)";

    /// <summary>The rules, in priority order.</summary>
    public static IReadOnlyList<RedactionRule> All { get; } =
    [
        // A terminated block, or, when no END line follows, the base64 and header lines after BEGIN.
        new("private-key",
            "-----BEGIN [A-Z ]{0,40}PRIVATE KEY-----(?:.{0,16384}?-----END [A-Z ]{0,40}PRIVATE KEY-----"
            + "|(?:\r?\n(?:[A-Za-z0-9+/=]{1,256}|Proc-Type:[^\r\n]{0,100}|DEK-Info:[^\r\n]{0,200})(?![^\r\n])){0,512})",
            RegexOptions.Singleline),
        new("anthropic-key", Start + "sk-ant-[A-Za-z0-9_-]{20,}"),
        new("openai-key", Start + "sk-(?:proj-)?[A-Za-z0-9_-]{20,}"),
        new("aws-access-key", Start + "A(?:KIA|SIA)[0-9A-Z]{16}" + End),
        new("github-token", Start + "(?:gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{22,})"),
        new("slack-token", Start + "xox[abprs]-[A-Za-z0-9-]{10,}"),
        new("stripe-key", Start + "[sr]k_(?:live|test)_[A-Za-z0-9]{16,}"),
        new("google-api-key", Start + "AIza[0-9A-Za-z_-]{35}" + End),
        new("linear-key", Start + "lin_(?:api|oauth)_[A-Za-z0-9]{32,}"),
        new("jwt", Start + "eyJ[A-Za-z0-9_-]{8,4096}\\.[A-Za-z0-9_-]{8,4096}\\.[A-Za-z0-9_-]{8,4096}"),
        new("bearer", Start + "Bearer[ \t]+(?<v>[A-Za-z0-9._~+/=-]{20,})", RegexOptions.IgnoreCase),
        new("credentialed-url",
            Start + "[a-z][a-z0-9+.-]{0,30}://[^ \t\r\n/:@]{1,256}:(?<v>[^ \t\r\n/@]{1,256})@", RegexOptions.IgnoreCase),

        // Code style: NAME [=:] "value", NAME ending in a secret word in any case; only a quoted value counts, so
        // member access and calls never do (refreshToken: RefreshTokenSchemaV2).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])[A-Za-z0-9_-]*?" + SecretWord + "['\"]?[ \t]*[=:][ \t]*['\"]" + Value, RegexOptions.IgnoreCase),

        // Env style: an UPPER_SNAKE name may take an unquoted value (API_KEY=..., CLIENT_SECRET=...).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])(?:[A-Z0-9]+_)*(?:API_?KEY|SECRET(?:_KEY)?|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_KEY)[ \t]*[=:][ \t]*"
            + Value),

        // Header style: a hyphenated header name may take an unquoted value (X-Api-Key: ...).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])(?:[A-Za-z0-9]+-)+(?:API-?KEY|SECRET|TOKEN|PASSWORD|ACCESS-?KEY|PRIVATE-?KEY):[ \t]*" + Value,
            RegexOptions.IgnoreCase),

        // ~/.aws/credentials (INI): these fixed keys at a line start take an unquoted value.
        new("secret-assignment",
            "^[ \t]*(?:aws_secret_access_key|aws_session_token)[ \t]*[=:][ \t]*(?<v>[A-Za-z0-9+/=]{16,})",
            RegexOptions.IgnoreCase | RegexOptions.Multiline),

        new("ssn", Start + "[0-9]{3}-[0-9]{2}-[0-9]{4}" + End),
        new("mrn", Start + "MRN[ \t:#-]*(?<v>[0-9]{6,10})" + End, RegexOptions.IgnoreCase),
    ];
}
