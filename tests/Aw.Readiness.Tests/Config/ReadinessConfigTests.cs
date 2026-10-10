using Aw.Readiness.Core;

namespace Aw.Readiness.Tests.Config;

public sealed class ReadinessConfigTests
{
    private const string Valid =
        "# readiness.yaml\n" +
        "version: 1\n" +
        "repo: agentic-workflow\n" +
        "ring: 0\n" +
        "scope: csharp-template   # the C# build surface\n" +
        "targets:\n" +
        "  - ring: 0\n" +
        "    level: 4\n" +
        "  - ring: 1\n" +
        "    level: 3\n";

    [Fact, Trait("Lane", "unit")]
    public void A_valid_file_parses_and_names_its_ring_target()
    {
        var config = ReadinessConfig.Parse(Valid);

        Assert.True(config.IsSuccess);
        Assert.Equal(RepoName.From("agentic-workflow"), config.Value.Repo);
        Assert.Equal("csharp-template", config.Value.Scope);
        Assert.Equal(new ReadinessTarget(0, 4), config.Value.Target);
        Assert.Equal(2, config.Value.Targets.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("version: 2", "version")]
    [InlineData("repo: Bad Name", "repo")]
    [InlineData("ring: x", "ring")]
    [InlineData("scope: typescript", "scope")]
    [InlineData("colour: blue", "colour")]
    [InlineData("    level: 9", "level")]
    public void A_bad_line_is_refused_with_the_line_and_the_key(string replacement, string key)
    {
        var lines = Valid.Split('\n');
        var index = Array.FindIndex(lines, l => l.TrimStart().StartsWith(key + ":", StringComparison.Ordinal));
        var text = index >= 0
            ? string.Join('\n', lines.Select((l, i) => i == index ? replacement : l))
            : Valid + "\n" + replacement;

        var config = ReadinessConfig.Parse(text);

        Assert.True(config.IsError);
        Assert.Equal("AW-READINESS-003", config.Error.Code);
        Assert.Contains(key, config.Error.Message, StringComparison.Ordinal);
        Assert.Contains("line", config.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("repo")]
    [InlineData("ring")]
    [InlineData("scope")]
    [InlineData("version")]
    public void A_missing_key_is_refused(string key)
    {
        var text = string.Join('\n', Valid.Split('\n').Where(l => !l.StartsWith(key + ":", StringComparison.Ordinal)));

        var config = ReadinessConfig.Parse(text);

        Assert.True(config.IsError);
        Assert.Contains(key, config.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ring_without_a_target_is_refused()
    {
        var config = ReadinessConfig.Parse(Valid.Replace("ring: 0\nscope", "ring: 2\nscope", StringComparison.Ordinal));

        Assert.True(config.IsError);
        Assert.Contains("ring 2", config.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("    level: 4\n    level: 2\n", "level")]
    [InlineData("    ring: 1\n", "ring")]
    public void A_key_repeated_in_one_target_is_refused(string extra, string key)
    {
        var config = ReadinessConfig.Parse(Valid.Replace("    level: 4\n", extra.StartsWith("    level", StringComparison.Ordinal) ? extra : "    level: 4\n" + extra, StringComparison.Ordinal));

        Assert.True(config.IsError);
        Assert.Contains($"'{key}' is set twice in this target", config.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unindented_target_list_names_the_indentation_fix()
    {
        var config = ReadinessConfig.Parse(Valid.Replace("  - ring: 0\n    level: 4", "- ring: 0\n  level: 4", StringComparison.Ordinal));

        Assert.True(config.IsError);
        Assert.Contains("indent target items with spaces under 'targets:'", config.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ring_listed_twice_is_refused()
    {
        var config = ReadinessConfig.Parse(Valid.Replace("ring: 1\n", "ring: 0\n", StringComparison.Ordinal));

        Assert.True(config.IsError);
        Assert.Contains("twice", config.Error.Message, StringComparison.Ordinal);
    }
}
