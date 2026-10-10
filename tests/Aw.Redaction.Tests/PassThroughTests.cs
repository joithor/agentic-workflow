namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed class PassThroughTests
{
    public static TheoryData<string> NotSecrets() =>
    [
        string.Empty,
        "The build passed on the second try; see the log.",
        "commit 0123456789abcdef0123456789abcdef01234567 fixed it",
        "see abc1234 for the fix",
        "digest 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        "refreshToken: RefreshTokenSchemaV2",
        "token = generateToken256Bits()",
        "const aws_session_token = getToken();",
        "password: hunter2",
        "secret = compute_hash_value_v2(input)",
        "token = session.refresh_token_value_2",
        "-----BEGIN RSA PRIVATE KEY----- is the header line",
        "C:\\new\\temp\\file.txt",
        "Bearer token expired",
        "\u043F\u0440\u0438\u0432\u0435\u0442 \u043C\u0438\u0440",
        "caf\u00E9 \uFF21\uFF22\uFF23 \u200D",
        "[REDACTED:github-token] was already scrubbed",
    ];

    [Theory]
    [MemberData(nameof(NotSecrets))]
    public void Text_without_secrets_comes_back_unchanged(string text)
    {
        var result = Redactor.Default.Scrub(text);

        Assert.Equal(ScrubOutcome.Clean, result.Outcome);
        Assert.Same(text, result.Text);
        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_long_run_of_one_letter_is_clean()
    {
        var text = new string('a', 20_000);

        var result = Redactor.Default.Scrub(text);

        Assert.Equal(ScrubOutcome.Clean, result.Outcome);
    }

    [Fact]
    public void A_long_hex_run_is_kept()
    {
        var text = string.Concat(Enumerable.Repeat("0123456789abcdef", 320));

        Assert.Equal(ScrubOutcome.Clean, Redactor.Default.Scrub(text).Outcome);
    }
}
