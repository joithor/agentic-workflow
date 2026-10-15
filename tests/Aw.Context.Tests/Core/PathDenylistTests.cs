using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class PathDenylistTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("config/.env.production")]
    [InlineData("certs/server.pem")]
    [InlineData("deploy/id_rsa")]
    [InlineData("infra/prod.tfstate")]
    [InlineData("a/b/credentials.json")]
    [InlineData("app/secrets/token.txt")]
    [InlineData("secrets/token.txt")]
    [InlineData(".npmrc")]
    public void The_built_in_secret_globs_deny_what_they_name(string path) =>
        Assert.True(PathDenylist.Matches(path, PathDenylist.Defaults));

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/app.ts")]
    [InlineData("README.md")]
    [InlineData("src/environment.ts")]
    [InlineData("docs/secrets-policy.md")]
    public void Ordinary_files_are_not_denied(string path) =>
        Assert.False(PathDenylist.Matches(path, PathDenylist.Defaults));

    [Theory, Trait("Lane", "unit")]
    [InlineData("**/fixtures/**", "src/Fixtures/a.ts", true)]
    [InlineData("**/fixtures/**", "fixtures/a/b.ts", true)]
    [InlineData("**/fixtures/**", "src/fixture/a.ts", false)]
    [InlineData("legacy/**", "legacy/old/a.ts", true)]
    [InlineData("legacy/**", "src/legacy/a.ts", false)]
    [InlineData("*.gen.ts", "a.gen.ts", true)]
    [InlineData("*.gen.ts", "src/a.gen.ts", false)]
    [InlineData("**/*.gen.ts", "src/deep/a.gen.ts", true)]
    [InlineData("src/?.ts", "src/a.ts", true)]
    [InlineData("src/?.ts", "src/ab.ts", false)]
    [InlineData("src/a+b(1).ts", "src/a+b(1).ts", true)]
    public void A_custom_glob_matches_without_regard_to_case_and_keeps_to_its_segments(string glob, string path, bool denied) =>
        Assert.Equal(denied, PathDenylist.Matches(path, [glob]));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_denies_through_its_own_list_and_the_default_list_is_the_built_ins()
    {
        var plain = new ContextRepo(ContextRepoName.From("app"), "/work/app");
        var custom = plain with { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };

        Assert.True(plain.Denies(".env"));
        Assert.False(plain.Denies("private/a.ts"));
        Assert.True(custom.Denies("private/a.ts"));
        Assert.True(custom.Denies("config/secrets/x.json"));
    }
}
