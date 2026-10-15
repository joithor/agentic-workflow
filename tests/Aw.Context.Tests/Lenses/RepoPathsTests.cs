using Aw.Context.Core.Lenses;

namespace Aw.Context.Tests.Lenses;

public sealed class RepoPathsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("./src", "src")]
    [InlineData("/src/", "src")]
    [InlineData("src//tabs", "src/tabs")]
    [InlineData(".", "")]
    [InlineData("src\\tabs", "src/tabs")]
    [InlineData("src/../lib", "lib")]
    [InlineData("src/tabs/../../lib/x", "lib/x")]
    [InlineData("../lib", "../lib")]
    [InlineData("src/../../lib", "../lib")]
    public void A_path_is_read_the_way_people_write_it(string written, string expected) => Assert.Equal(expected, RepoPaths.Normalize(written));
}
