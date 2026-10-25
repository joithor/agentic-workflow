using Aw.Context.Core.Code;

namespace Aw.Context.Tests.Code;

public sealed class SiblingRuleTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/links.test.ts", "src/sharing/links.ts")]
    [InlineData("src/sharing/links.spec.ts", "src/sharing/links.ts")]
    [InlineData("pkg/links_test.go", "pkg/links.go")]
    [InlineData("tests/test_links.py", "src/links.py")]
    [InlineData("tests/LinkServiceTests.cs", "src/LinkService.cs")]
    [InlineData("tests/LinkServiceTest.java", "src/LinkService.java")]
    [InlineData("tests/LINKS.test.ts", "src/links.ts")]
    [InlineData("tests/foo.service.spec.ts", "src/foo.service.ts")]
    [InlineData("spec/links_spec.rb", "lib/links.rb")]
    [InlineData("tests/LinkServiceSpec.scala", "src/LinkService.scala")]
    [InlineData("tests/links_tests.py", "src/links.py")]
    public void SiblingRule_a_test_stem_without_its_marker_ties_the_implementing_stem(string test, string impl) =>
        Assert.True(SiblingRule.Ties(test, impl));

    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/index.test.ts", "src/index.ts")]
    [InlineData("tests/main_test.go", "cmd/main.go")]
    [InlineData("tests/utils.test.ts", "src/utils.ts")]
    [InlineData("tests/types.spec.ts", "src/types.ts")]
    [InlineData("tests/__init__.py", "src/__init__.py")]
    [InlineData("tests/links.test.ts", "src/link.ts")]
    [InlineData("tests/latest.ts", "src/la.ts")]
    public void SiblingRule_generic_or_different_stems_tie_nothing(string test, string impl) =>
        Assert.False(SiblingRule.Ties(test, impl));
}
