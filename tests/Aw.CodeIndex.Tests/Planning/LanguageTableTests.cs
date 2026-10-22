using Aw.CodeIndex.Core.Planning;

namespace Aw.CodeIndex.Tests.Planning;

public sealed class LanguageTableTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("src/A.cs", "csharp")]
    [InlineData("web/app.TSX", "typescript")]
    [InlineData("tools/run.py", "python")]
    [InlineData("a/b.mjs", "javascript")]
    [InlineData("README.md", null)]
    [InlineData("Makefile", null)]
    [InlineData("dir.cs/readme", null)]
    public void A_path_names_its_language_by_extension_or_none(string path, string? language)
        => Assert.Equal(language, LanguageTable.Of(path));

    [Fact, Trait("Lane", "unit")]
    public void Manifest_directories_are_those_holding_a_csproj_or_a_known_manifest()
    {
        var directories = ProjectManifests.Directories(["Directory.Build.props", "src/A/A.csproj", "src/A/Code.cs", "web/package.json", "go.mod", "docs/readme.md"]);

        Assert.Equal(["", "src/A", "web"], directories.Order(StringComparer.Ordinal));
    }
}
