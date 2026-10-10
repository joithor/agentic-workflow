namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class NormalizationTests
{
    [Theory]
    [InlineData("at 2026-10-10T06:00:00.123Z ok", "at <timestamp> ok")]
    [InlineData("at 2026-10-10T06:00:00Z", "at <timestamp>")]
    [InlineData("at 2026-10-10T08:00:00+02:00", "at <timestamp>")]
    public void Timestamps_become_a_placeholder(string text, string expected) =>
        Assert.Equal(expected, new Normalization(Normalization.IsoTimestamps).Apply(text));

    [Fact]
    public void Guids_and_ulids_become_placeholders()
    {
        var n = new Normalization(Normalization.Guids, Normalization.Ulids);

        Assert.Equal(
            "id <guid> run <ulid>",
            n.Apply("id 0e5f38f5-1c2d-4e3f-8a9b-0123456789ab run 01J9Z3M4N5P6Q7R8S9T0V1W2X3"));
    }

    [Fact]
    public void A_ulid_inside_a_longer_word_is_left_alone() =>
        Assert.Equal(
            "x01J9Z3M4N5P6Q7R8S9T0V1W2X3",
            new Normalization(Normalization.Ulids).Apply("x01J9Z3M4N5P6Q7R8S9T0V1W2X3"));

    [Fact]
    public void Durations_become_a_placeholder() =>
        Assert.Equal("took <duration>, then <duration>", new Normalization(Normalization.Durations).Apply("took 12ms, then 1.5s"));

    [Fact]
    public void A_temp_path_becomes_a_placeholder_even_with_regex_characters() =>
        Assert.Equal(
            "wrote <tmp>/out.json",
            new Normalization(Normalization.TempPath("/tmp/a+b (1)")).Apply("wrote /tmp/a+b (1)/out.json"));

    [Fact]
    public void Rules_apply_in_declared_order()
    {
        var first = new NormalizationRule("first", "abc", "X");
        var second = new NormalizationRule("second", "X", "Y");

        Assert.Equal("Y", new Normalization(first, second).Apply("abc"));
        Assert.Equal("X", new Normalization(second, first).Apply("abc"));
    }

    [Fact]
    public void Line_endings_are_always_normalized() =>
        Assert.Equal("a\nb\n", Normalization.None.Apply("a\r\nb\r\n"));

    [Fact]
    public void An_invalid_pattern_is_refused_when_the_rule_is_made() =>
        Assert.ThrowsAny<ArgumentException>(() => new NormalizationRule("bad", "(", "x"));

    [Fact]
    public void The_declared_rules_are_visible_by_name() =>
        Assert.Equal(
            "isoTimestamps,guids",
            string.Join(',', new Normalization(Normalization.IsoTimestamps, Normalization.Guids).Rules.Select(r => r.Name)));
}
