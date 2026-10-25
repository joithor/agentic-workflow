using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Values;

public sealed class ValueObjectTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("agentic-workflow", true)]
    [InlineData("a.b", true)]
    [InlineData("_x", true)]
    [InlineData(".hidden", false)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("a b", false)]
    [InlineData("..", false)]
    public void RepoName_accepts_only_a_file_safe_name(string text, bool valid)
        => Assert.Equal(valid, RepoName.TryFrom(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void RepoName_is_at_most_64_characters()
    {
        Assert.True(RepoName.TryFrom(new string('a', 64), out _));
        Assert.False(RepoName.TryFrom(new string('a', 65), out _));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567", false)]
    [InlineData("0123456789abcdef", false)]
    [InlineData("main", false)]
    public void CommitSha_is_40_lower_case_hex_digits(string text, bool valid)
        => Assert.Equal(valid, CommitSha.TryFrom(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData("repo:src/A.cs#Ns.A.Run/2@ab12", true)]
    [InlineData("repo:a#b", true)]
    [InlineData("repo:src/A.cs", false)]
    [InlineData("src/A.cs#Run", false)]
    [InlineData("repo:src/A.cs#Run\n", false)]
    [InlineData("", false)]
    public void SymbolRef_has_a_repo_a_path_and_a_qualified_name(string text, bool valid)
        => Assert.Equal(valid, SymbolRef.TryFrom(text, out _));
}
