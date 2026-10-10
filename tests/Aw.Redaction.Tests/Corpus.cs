using static Aw.Redaction.Tests.Shapes;

namespace Aw.Redaction.Tests;

/// <summary>The secret shapes of the A3 differential (judge redactSecrets vs sindri scrub), with the gaps neither
/// TS scrubber closed: escaped newlines, zero-width joiners, look-alike prefixes and unterminated key blocks.</summary>
public static class Corpus
{
    public static TheoryData<SecretCase> Plain() =>
    [
        new SecretCase("openai-key", Join("key=", "sk-", Body, Body), "openai-key", Tail),
        new SecretCase("openai-proj-key", Join("sk-", "proj-", Body, Body), "openai-key", Tail),
        new SecretCase("anthropic-key", Join("sk-", "ant-", Body, Body), "anthropic-key", Tail),
        new SecretCase("github-pat-classic", Join("token gh", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("github-pat-fine", Join("github", "_pat_", Body, Body), "github-token", Tail),
        new SecretCase("github-oauth", Join("gh", "o_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("aws-access-key", Aws("AK" + "IA"), "aws-access-key", "IJKLMNOP"),
        new SecretCase("aws-sts-key", Aws("AS" + "IA"), "aws-access-key", "IJKLMNOP"),
        new SecretCase("slack-token", Join("xo", "xb-1234567890-", Body), "slack-token", Tail),
        new SecretCase("stripe-live", Join("sk", "_live_", Body), "stripe-key", Tail),
        new SecretCase("google-api-key", Join("AI", "za", Body, "Ab3dE6gH"), "google-api-key", Tail),
        new SecretCase("linear-key", Join("lin", "_api_", Body, Body), "linear-key", Tail),
        new SecretCase("jwt", Join("ey", "J", Body, ".", Body, ".", Body), "jwt", Tail),
        new SecretCase("bearer-header", Join("Authorization: Bear", "er ", Body, Body), "bearer", Tail),
        new SecretCase("credentialed-url", Join("https://deploy:", Body, "@example.com/repo.git"), "credentialed-url", Tail),
        new SecretCase("env-assignment", Join("API", "_KEY=", Body, "Ab37"), "secret-assignment", Tail),
        new SecretCase("env-client-secret", Join("CLIENT", "_SECRET=", Body, "9"), "secret-assignment", Tail),
        new SecretCase("json-client-secret", Join("{\"client", "_secret\": \"", Body, "Ab37\"}"), "secret-assignment", Tail),
        new SecretCase("header-api-key", Join("X-Api", "-Key: ", Body, "Ab37"), "secret-assignment", Tail),
        new SecretCase("aws-ini", Join("aws_secret", "_access_key = ", Body, Body), "secret-assignment", Tail),
        new SecretCase("ssn", "SSN 123-45-6789 on file", "ssn", "45-6789"),
        new SecretCase("mrn", "MRN: 12345678", "mrn", "12345678"),
        new SecretCase("private-key", Pem("-----END RSA PRIV" + "ATE KEY-----"), "private-key", Tail),
        new SecretCase("code-style-next-line", Join("const githubTo", "ken =\n  \"", Body, "9\";"), "secret-assignment", Tail),
        new SecretCase("env-next-line", Join("API", "_KEY=\n", Body, "9"), "secret-assignment", Tail),
        new SecretCase("bearer-next-line", Join("Authorization: Bear", "er\n", Body, Body), "bearer", Tail),
        new SecretCase("mrn-next-line", "MRN:\n12345678", "mrn", "12345678"),
        new SecretCase("export-lowercase-midline", Join("cd repo && export api", "_key=", Body), "secret-assignment", Tail),
        new SecretCase("pgp-private-key", Join(
            "-----BEGIN PGP PRIV", "ATE KEY BLOCK-----\n\n", Body, Body, "\n=Ab3d\n-----END PGP PRIV", "ATE KEY BLOCK-----"),
            "private-key", Tail),
    ];

    public static TheoryData<SecretCase> Evasions() =>
    [
        new SecretCase("unterminated-private-key", Pem(string.Empty), "private-key", Tail),
        new SecretCase("non-breaking-space-bearer", Join("Bear", "er\u00A0", Body, Body), "bearer", Tail),
        new SecretCase("cyrillic-lookalike", Join("token g\u04BB", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("fullwidth-prefix", Join("token \uFF47\uFF48", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("greek-lookalike-aws", Aws("\u0391\u039A" + "\u0399\u0391"), "aws-access-key", "IJKLMNOP"),
        new SecretCase("cyrillic-stripe", Join("\u0455k", "_live_", Body), "stripe-key", Tail),
        new SecretCase("zero-width-joiner", Join("token gh", "p_", "Ab3dE6gH9jK2mN5p", "\u200D", Body), "github-token", Tail),
        new SecretCase("zero-width-space-prefix", Join("gh\u200B", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("soft-hyphen", Join("sk\u00AD", "_live_", Body), "stripe-key", Tail),
        new SecretCase("multiline-json-escape", Join("{\"note\":\"line1\\ngh", "p_", Body, "Ab3dE6gH9\"}"), "github-token", Tail),
        new SecretCase("json-tab-escape", Join("{\"k\":\"a\\tsk-", "ant-", Body, Body, "\"}"), "anthropic-key", Tail),
        new SecretCase("json-unicode-zwj", Join("gh", "p_", "Ab3dE6gH9jK2mN5p", "\\u200d", Body), "github-token", Tail),
        new SecretCase("escaped-json-assignment", Join("{\\\"client", "_secret\\\": \\\"", Body, "Ab37\\\"}"), "secret-assignment", Tail),
        new SecretCase("unterminated-encrypted-key", Join(
            "-----BEGIN RSA PRIV", "ATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,ABCDEF\n\n", Body, Body, "\n"),
            "private-key", Tail),
        new SecretCase("ini-lowercase", Join("api", "_key=", Body, "Ab37"), "secret-assignment", Tail),
        new SecretCase("yaml-client-secret", Join("  client", "_secret: ", Body, "9  # rotated"), "secret-assignment", Tail),
        new SecretCase("properties-dotted", Join("db.pass", "word = ", Body, "\r\nnext=1"), "secret-assignment", Tail),
        new SecretCase("escaped-private-key", Pem("-----END RSA PRIV" + "ATE KEY-----").Replace("\n", "\\n", StringComparison.Ordinal), "private-key", Tail),
        new SecretCase("crlf-private-key", Pem("-----END RSA PRIV" + "ATE KEY-----").Replace("\n", "\r\n", StringComparison.Ordinal), "private-key", Tail),
        new SecretCase("crlf-unterminated-private-key", Pem(string.Empty).Replace("\n", "\r\n", StringComparison.Ordinal), "private-key", Tail),
        new SecretCase("unterminated-key-one-long-line", Join(
            "-----BEGIN RSA PRIV", "ATE KEY-----\n", string.Concat(Enumerable.Repeat(Body, 80))), "private-key", Tail),
        new SecretCase("unterminated-key-space-separated", Join("-----BEGIN RSA PRIV", "ATE KEY----- ", Body, " ", Body), "private-key", Tail),
        new SecretCase("unterminated-key-on-begin-line", Join("-----BEGIN RSA PRIV", "ATE KEY-----", Body, Body), "private-key", Tail),
        new SecretCase("cyrillic-text-glued", Join("\u0442\u043E\u043A\u0435\u043D\u0430gh", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("fullwidth-letter-glued", Join("\uFF41gh", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("aws-cyrillic-after", Aws("AK" + "IA") + "\u043E", "aws-access-key", "IJKLMNOP"),
        new SecretCase("windows-path-backslash-r", Join("C:\\keys\\", "rk", "_live_", Body), "stripe-key", Tail),
    ];
}
