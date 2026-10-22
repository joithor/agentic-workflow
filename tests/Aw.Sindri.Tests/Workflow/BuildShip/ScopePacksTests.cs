using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>A repo's packs (.agentic-workflow/packs) are outside every phase's write scope (line spec §5.1), however broad its allowedFiles.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopePacksTests
{
    [Theory]
    [InlineData(".agentic-workflow/packs/lenses/house/pack.json", true)]
    [InlineData(".agentic-workflow/packs/style/plain/examples/a.test.md", true)]
    [InlineData(".agentic-workflow/packs", true)]
    [InlineData(".agentic-workflow/evidence.json", false)]
    [InlineData("src/.agentic-workflow/packs/x", false)]
    [InlineData("docs/packs/a.md", false)]
    public void Only_paths_under_the_repo_root_packs_directory_are_pack_paths(string path, bool isPack) => Assert.Equal(isPack, ScopeGlobs.IsPackPath(path));

    [Theory]
    [InlineData("**")]
    [InlineData(".agentic-workflow/**")]
    [InlineData(".agentic-workflow/packs/**")]
    [InlineData("new: .agentic-workflow/packs/lenses/house/pack.json")]
    public void No_allowed_files_entry_allows_a_pack_path(string entry) =>
        Assert.False(ScopeGlobs.Allows([entry], ".agentic-workflow/packs/lenses/house/pack.json"));

    [Fact]
    public void A_broad_glob_still_allows_everything_else()
    {
        Assert.True(ScopeGlobs.Allows(["**"], "src/a.cs"));
        Assert.True(ScopeGlobs.Allows([".agentic-workflow/**"], ".agentic-workflow/evidence.json"));
    }

    [Fact]
    public void A_new_test_file_under_packs_is_still_out_of_scope()
    {
        var scope = new PhaseScope("b0", ["**"], []);
        var changes = new BranchChanges(
            [new ChangedFile(".agentic-workflow/packs/lenses/house/a.test.ts", ChangeKind.Added), new ChangedFile("src/ok.cs", ChangeKind.Added)], [], []);

        var findings = BranchRules.OutOfScope(scope, changes);

        Assert.Equal([".agentic-workflow/packs/lenses/house/a.test.ts"], findings.Select(static f => f.Path), StringComparer.Ordinal);
    }
}
