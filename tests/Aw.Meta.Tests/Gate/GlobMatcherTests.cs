using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class GlobMatcherTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("**", "a/b/c.txt", true)]
    [InlineData("**", "x", true)]
    [InlineData("docs/**", "docs/a/b.md", true)]
    [InlineData("docs/**", "docs/a.md", true)]
    [InlineData("docs/**", "src/docs/a.md", false)]
    [InlineData("**/*.md", "README.md", true)]
    [InlineData("**/*.md", "a/b/README.md", true)]
    [InlineData("**/*.md", "a/b/README.txt", false)]
    [InlineData("*.props", "Directory.Build.props", true)]
    [InlineData("*.props", "src/Directory.Build.props", false)]
    [InlineData("src/*/x.cs", "src/a/x.cs", true)]
    [InlineData("src/*/x.cs", "src/a/b/x.cs", false)]
    [InlineData("src/**/x.cs", "src/a/b/x.cs", true)]
    [InlineData("src/**/x.cs", "src/x.cs", true)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "a/c", false)]
    [InlineData("global.json", "global.json", true)]
    [InlineData("global.json", "global.jsonx", false)]
    [InlineData("", "a", false)]
    public void Patterns_match_paths(string pattern, string path, bool expected) =>
        Assert.Equal(expected, GlobMatcher.IsMatch(pattern, path));
}
