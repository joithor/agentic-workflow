using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Data;

public sealed class CliGuardTests
{
    private const string Repo = "/work/repo";

    private static CliStep Call(params string[] argv) => new(argv, new Expectation(ExitCode: 0));

    private static ResolvedTargets Outputs(params string[] buildOutputs) => new(buildOutputs, [], []);

    [Fact, Trait("Lane", "unit")]
    public void A_target_inside_a_build_output_resolves_to_its_real_path()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out").Executable("/work/repo/out/tool");

        var real = CliGuard.ResolveTarget(Call("out/tool", "list"), Repo, Outputs("out"), locator);

        Assert.Equal("/work/repo/out/tool", real.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_name_is_refused_and_never_looked_up()
    {
        var locator = new FakeExecutableLocator().Executable("/usr/bin/tool");

        var real = CliGuard.ResolveTarget(Call("tool"), Repo, Outputs("out"), locator);

        Assert.Equal(EvidenceErrors.CliRefused.Code, real.Error.Code);
        Assert.Contains("PATH", real.Error.Message, StringComparison.Ordinal);
        Assert.Empty(locator.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_that_climbs_out_of_the_build_output_is_refused()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out").Executable("/work/repo/out/../../etc/tool", "/etc/tool").Executable("/etc/tool");

        var real = CliGuard.ResolveTarget(Call("out/../../etc/tool"), Repo, Outputs("out"), locator);

        Assert.Equal(EvidenceErrors.CliRefused.Code, real.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_symlink_that_leaves_the_build_output_is_refused()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out").Executable("/work/repo/out/tool", "/usr/bin/other");

        var real = CliGuard.ResolveTarget(Call("out/tool"), Repo, Outputs("out"), locator);

        Assert.Equal(EvidenceErrors.CliRefused.Code, real.Error.Code);
        Assert.Contains("/usr/bin/other", real.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directory_whose_name_only_starts_like_the_build_output_is_outside_it()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out").Executable("/work/repo/out/tool", "/work/repo/out-evil/tool");

        Assert.True(CliGuard.ResolveTarget(Call("out/tool"), Repo, Outputs("out"), locator).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_symlinked_build_output_is_compared_by_its_real_path()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out", "/work/repo/build/out").Executable("/work/repo/out/tool", "/work/repo/build/out/tool");

        Assert.Equal("/work/repo/build/out/tool", CliGuard.ResolveTarget(Call("out/tool"), Repo, Outputs("out"), locator).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_allowlisted_program_is_accepted_by_its_real_path()
    {
        var locator = new FakeExecutableLocator().Executable("/opt/tool-link", "/opt/tool").Executable("/opt/tool");
        var targets = new ResolvedTargets([], ["/opt/tool-link"], []);

        Assert.Equal("/opt/tool", CliGuard.ResolveTarget(Call("/opt/tool-link"), Repo, targets, locator).Value);
        Assert.Equal("/opt/tool", CliGuard.ResolveTarget(Call("/opt/tool"), Repo, targets, locator).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_that_is_not_an_executable_file_is_refused()
    {
        var locator = new FakeExecutableLocator().Directory("/work/repo/out");

        var real = CliGuard.ResolveTarget(Call("out/missing"), Repo, Outputs("out"), locator);

        Assert.Contains("not an executable file", real.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_configured_the_refusal_names_the_file_to_write()
    {
        var locator = new FakeExecutableLocator().Executable("/work/repo/out/tool");

        var real = CliGuard.ResolveTarget(Call("out/tool"), Repo, ResolvedTargets.Empty, locator);

        Assert.Equal(EvidenceErrors.CliRefused.Code, real.Error.Code);
        Assert.Contains("/work/repo/.agentic-workflow/evidence.json", real.Error.Message, StringComparison.Ordinal);
        Assert.Contains("cli.buildOutputs", real.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("delete")]
    [InlineData("push")]
    [InlineData("rm")]
    [InlineData("drop")]
    [InlineData("--force-push")]
    [InlineData("deleteAll")]
    [InlineData("save-item")]
    public void An_argument_that_reads_as_a_write_verb_is_refused_unless_declared(string argument)
    {
        var call = Call("out/tool", "items", argument);

        Assert.Equal(EvidenceErrors.CliRefused.Code, CliGuard.CheckWrites(call, []).Error.Code);
        Assert.True(CliGuard.CheckWrites(call, ["delete", "push", "rm", "drop", "force", "all", "save"]).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_refusal_names_the_word_not_the_argument()
    {
        var error = CliGuard.CheckWrites(Call("out/tool", "delete=abc-secret-value"), []).Error;

        Assert.Contains("'delete'", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abc-secret-value", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Reading_arguments_and_the_program_name_are_not_writes() =>
        Assert.True(CliGuard.CheckWrites(Call("out/delete-tool", "list", "--limit", "5"), []).IsSuccess);

    [Theory, Trait("Lane", "unit")]
    [InlineData("GH_TOKEN")]
    [InlineData("GITHUB_ACTIONS")]
    [InlineData("AWS_REGION")]
    [InlineData("AZURE_CLIENT_ID")]
    [InlineData("GCP_PROJECT")]
    [InlineData("GOOGLE_APPLICATION_CREDENTIALS")]
    [InlineData("SSH_AUTH_SOCK")]
    [InlineData("DATABASE_URL")]
    [InlineData("MY_PASSWORD")]
    [InlineData("STRIPE_API_KEY")]
    [InlineData("SIGNING_PRIVATE_KEY")]
    [InlineData("SOME_SECRET")]
    [InlineData("CREDENTIALS_FILE")]
    public void A_pass_through_name_that_reads_as_a_secret_is_refused(string name)
    {
        var error = CliGuard.CheckPassEnv([name]).Error;

        Assert.Equal(EvidenceErrors.CliRefused.Code, error.Code);
        Assert.Contains(name, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ordinary_names_pass_through() =>
        Assert.True(CliGuard.CheckPassEnv(["LANG", "NOTES_REGION", "TZ"]).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void A_step_target_keeps_the_program_and_its_words_and_hides_every_other_value() =>
        Assert.Equal("out/notes list --limit <value> <value> <value>", CliGuard.TargetText(Call("out/notes", "list", "--limit", "5", "a b", "--limit=5")));
}
