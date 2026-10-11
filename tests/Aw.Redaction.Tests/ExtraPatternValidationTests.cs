namespace Aw.Redaction.Tests;

/// <summary>Extra patterns that break Redaction's pattern rules are refused with a typed error naming the entry and
/// the field (context spec §14 S-L3, A-F16), so a caller can report the key path.</summary>
[Trait("Lane", "unit")]
public sealed class ExtraPatternValidationTests
{
    private const string Good = "ACME-[0-9]{6}";

    public static TheoryData<string> MalformedIds() =>
    [
        string.Empty,
        "Acme-customer-id",
        "acme/customer-id",
        "acme customer",
        "acme_customer",
        "-acme",
        "acme-",
        "acme--id",
        "1acme",
        "acme]",
        "acme-customer-id\n",
        "a" + new string('b', 64),
    ];

    public static TheoryData<string> ReservedIds() =>
        ["secret-assignment", "github-token", "private-key", "blocked", "secret-field"];

    public static TheoryData<string> InvalidPatterns() =>
    [
        string.Empty,
        "ACME-[0-9",
        "(?<v>ACME",
        "ACME-[0-9]{6})",
        "A" + new string('B', 1024),
    ];

    public static TheoryData<string> UnicodeShorthands() =>
        ["ACME-\\d{6}", "ACME-\\D", "\\w+-[0-9]{6}", "ACME\\W", "ACME\\s[0-9]{6}", "ACME\\S", "\\bACME-[0-9]{6}", "ACME\\B", "ACME-[\\d]{6}"];

    [Theory]
    [MemberData(nameof(MalformedIds))]
    public void A_malformed_id_is_refused(string id) =>
        _ = AssertRefused(Redactor.Create([new(id, Good)], Redactor.DefaultMatchTimeout), 0, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);

    [Theory]
    [MemberData(nameof(ReservedIds))]
    public void A_built_in_or_reserved_id_is_refused(string id)
    {
        var result = Redactor.Create([new(id, Good)], Redactor.DefaultMatchTimeout);

        var error = AssertRefused(result, 0, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);
        Assert.Contains(id, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_repeated_id_is_refused_at_its_second_entry()
    {
        var result = Redactor.Create([new("acme-id", Good), new("acme-id", "ACME-[A-Z]{3}")], Redactor.DefaultMatchTimeout);

        _ = AssertRefused(result, 1, ExtraPatternField.Id, RedactionCodes.InvalidPatternId);
    }

    [Theory]
    [MemberData(nameof(InvalidPatterns))]
    public void An_empty_overlong_or_unparsable_pattern_is_refused(string pattern) =>
        _ = AssertRefused(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout), 0, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern);

    [Theory]
    [MemberData(nameof(UnicodeShorthands))]
    public void A_unicode_aware_shorthand_is_refused_with_its_ascii_replacement(string pattern)
    {
        var result = Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout);

        var error = AssertRefused(result, 0, ExtraPatternField.Pattern, RedactionCodes.InvalidPattern);
        Assert.Contains("ASCII", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C:\\\\dir")]
    [InlineData("ACME-[0-9]{6}\\.\\-")]
    [InlineData("(?i)acme-[0-9]{6}")]
    [InlineData("(?<![A-Za-z0-9_])ACME-[0-9]{6}(?![A-Za-z0-9_])")]
    public void A_pattern_with_explicit_classes_and_escaped_backslashes_is_accepted(string pattern) =>
        Assert.True(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout).IsValid);

    [Fact]
    public void The_message_never_quotes_the_pattern()
    {
        var pattern = "SECRETISH-[0-9";

        var error = Assert.Single(Redactor.Create([new("acme-id", pattern)], Redactor.DefaultMatchTimeout).Errors);

        Assert.DoesNotContain("SECRETISH", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_refused_field_is_reported_in_entry_order()
    {
        ExtraPattern[] extras =
        [
            new("acme-id", Good),
            new("Bad/Id", "ACME-\\d"),
            new("other-id", "("),
        ];

        var result = Redactor.Create(extras, Redactor.DefaultMatchTimeout);

        Assert.False(result.IsValid);
        Assert.Null(result.Redactor);
        Assert.Equal(
            [(1, ExtraPatternField.Id), (1, ExtraPatternField.Pattern), (2, ExtraPatternField.Pattern)],
            result.Errors.Select(e => (e.Index, e.Field)));
    }

    private static ExtraPatternError AssertRefused(RedactorResult result, int index, ExtraPatternField field, string code)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Redactor);
        var error = Assert.Single(result.Errors);
        Assert.Equal((index, field, code), (error.Index, error.Field, error.Code));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }
}
