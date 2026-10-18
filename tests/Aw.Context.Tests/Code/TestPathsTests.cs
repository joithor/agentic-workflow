using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Code;

public sealed class TestPathsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/sharing.test.ts", true)]
    [InlineData("src/sharing/links.spec.ts", true)]
    [InlineData("src/sharing/test/helper.ts", true)]
    [InlineData("src/__tests__/links.ts", true)]
    [InlineData("pkg/links_test.go", true)]
    [InlineData("src/LinkServiceTests.cs", true)]
    [InlineData("src/LinkServiceTest.java", true)]
    [InlineData("tests/test_links.py", true)]
    [InlineData("src/sharing/links.ts", false)]
    [InlineData("src/sharing/testing.ts", false)]
    [InlineData("src/FooTest.cs", true)]
    [InlineData("src/foo.test.ts", true)]
    [InlineData("pkg/foo_test.go", true)]
    [InlineData("src/Links.Tests/Links.cs", false)]
    [InlineData("Tests/Links.cs", true)]
    [InlineData("src/latest.ts", false)]
    [InlineData("src/contest.js", false)]
    [InlineData("src/attest.py", false)]
    public void TestPath_default_globs_mark_the_usual_test_files(string path, bool expected) =>
        Assert.Equal(expected, TestPaths.IsTest(path, ContextSettings.DefaultTestGlobs));

    [Fact, Trait("Lane", "unit")]
    public void TestPath_a_profile_override_replaces_the_defaults()
    {
        string[] globs = ["checks/**"];

        Assert.True(TestPaths.IsTest("checks/links.ts", globs));
        Assert.False(TestPaths.IsTest("tests/links.test.ts", globs));
    }

    [Fact, Trait("Lane", "unit")]
    public void TestPath_globs_match_case_sensitively()
    {
        Assert.False(TestPaths.IsTest("src/Links.TEST.ts", ContextSettings.DefaultTestGlobs));
        Assert.False(TestPaths.IsTest("src/TESTS/links.ts", ContextSettings.DefaultTestGlobs));
    }

    [Fact, Trait("Lane", "unit")]
    public void TestPath_no_globs_mark_nothing() =>
        Assert.False(TestPaths.IsTest("tests/links.test.ts", []));
}
