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
    ];

    public static TheoryData<SecretCase> Evasions() =>
    [
        new SecretCase("unterminated-private-key", Pem(string.Empty), "private-key", Tail),
        new SecretCase("non-breaking-space-bearer", Join("Bear", "er ", Body, Body), "bearer", Tail),
        new SecretCase("cyrillic-lookalike", Join("token gһ", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("fullwidth-prefix", Join("token ｇｈ", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("greek-lookalike-aws", Aws("ΑΚ" + "ΙΑ"), "aws-access-key", "IJKLMNOP"),
        new SecretCase("cyrillic-stripe", Join("ѕk", "_live_", Body), "stripe-key", Tail),
        new SecretCase("zero-width-joiner", Join("token gh", "p_", "Ab3dE6gH9jK2mN5p", "‍", Body), "github-token", Tail),
        new SecretCase("zero-width-space-prefix", Join("gh​", "p_", Body, "Ab3dE6gH9"), "github-token", Tail),
        new SecretCase("soft-hyphen", Join("sk­", "_live_", Body), "stripe-key", Tail),
        new SecretCase("multiline-json-escape", Join("{\"note\":\"line1\\ngh", "p_", Body, "Ab3dE6gH9\"}"), "github-token", Tail),
        new SecretCase("json-tab-escape", Join("{\"k\":\"a\\tsk-", "ant-", Body, Body, "\"}"), "anthropic-key", Tail),
        new SecretCase("json-unicode-zwj", Join("gh", "p_", "Ab3dE6gH9jK2mN5p", "\\u200d", Body), "github-token", Tail),
        new SecretCase("escaped-json-assignment", Join("{\\\"client", "_secret\\\": \\\"", Body, "Ab37\\\"}"), "secret-assignment", Tail),
        new SecretCase("escaped-private-key", Pem("-----END RSA PRIV" + "ATE KEY-----").Replace("\n", "\\n", StringComparison.Ordinal), "private-key", Tail),
    ];
}
