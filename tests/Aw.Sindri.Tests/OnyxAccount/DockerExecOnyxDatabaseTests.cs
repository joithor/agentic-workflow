using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// The one read of Sindri's own Onyx database: which commands run, that it can't write, and what every way of failing says.
public sealed class DockerExecOnyxDatabaseTests
{
    private static readonly ComposeProject _project = ComposeProject.ByLabel(ComposeProject.NameFor("/sindri-home"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Out(string output) => new(ToolOutcome.Exited, 0, output);

    private static ToolRun Failed(string error = "") => new(ToolOutcome.Exited, 1, string.Empty, error);

    private static (DockerExecOnyxDatabase Database, FakeToolRunner Runner) Over(ToolRun ps, ToolRun exec)
    {
        var runner = new FakeToolRunner((_, arguments) => arguments[0] == "ps" ? ps : exec);
        return (new DockerExecOnyxDatabase(runner), runner);
    }

    [Fact, Trait("Lane", "unit")]
    public void It_finds_the_database_container_by_the_project_and_service_labels_then_reads_one_fixed_query_read_only()
    {
        var (database, runner) = Over(Out("0123456789ab\n"), Out("2\n"));

        var group = database.DefaultAdminGroup(_project, Token);

        Assert.Equal(2, group.Value);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(["ps", "-q", "--filter", $"label=com.docker.compose.project={_project.Name}", "--filter", "label=com.docker.compose.service=relational_db"], runner.Calls[0].Arguments);
        Assert.Equal(
            ["exec", "-e", "PGOPTIONS=-c default_transaction_read_only=on", "0123456789ab", "psql", "-X", "-U", "postgres", "-tA", "-c", "select id from user_group where is_default and name = 'Admin'"],
            runner.Calls[1].Arguments);
        Assert.All(runner.Calls, static c => Assert.Equal("docker", c.FileName));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_sindri_did_not_name_is_refused_before_any_command_runs()
    {
        var (database, runner) = Over(Out("0123456789ab\n"), Out("2\n"));

        var group = database.DefaultAdminGroup(ComposeProject.ByLabel("other"), Token);

        Assert.Equal(SindriErrors.OnyxComposeFailed.Code, group.Error.Code);
        Assert.Empty(runner.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_running_database_container_is_null_and_no_query_runs()
    {
        var (database, runner) = Over(Out(string.Empty), Out("2\n"));

        var group = database.DefaultAdminGroup(_project, Token);

        Assert.Null(group.Value);
        _ = Assert.Single(runner.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void Docker_that_is_not_installed_is_sindri_onyx_001()
    {
        var (database, _) = Over(ToolRun.Missing, ToolRun.Missing);

        Assert.Equal(SindriErrors.OnyxDockerUnavailable.Code, database.DefaultAdminGroup(_project, Token).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ps")]
    [InlineData("exec")]
    public void A_docker_command_that_fails_is_sindri_onyx_011_with_what_it_printed(string failing)
    {
        var (database, _) = Over(failing == "ps" ? Failed("ps broke") : Out("0123456789ab\n"), Failed("exec broke"));

        var group = database.DefaultAdminGroup(_project, Token);

        Assert.Equal(SindriErrors.OnyxComposeFailed.Code, group.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_database_containers_are_refused_rather_than_guessed_between()
    {
        var (database, runner) = Over(Out("0123456789ab\nba9876543210\n"), Out("2\n"));

        Assert.Equal(SindriErrors.OnyxComposeFailed.Code, database.DefaultAdminGroup(_project, Token).Error.Code);
        _ = Assert.Single(runner.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void Something_that_is_not_a_container_id_is_never_put_in_a_command()
    {
        var (database, runner) = Over(Out("--privileged\n"), Out("2\n"));

        Assert.Equal(SindriErrors.OnyxComposeFailed.Code, database.DefaultAdminGroup(_project, Token).Error.Code);
        _ = Assert.Single(runner.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("1\n2\n")]
    [InlineData("Admin\n")]
    [InlineData("0\n")]
    public void An_answer_that_is_not_one_positive_id_is_sindri_onyx_008(string printed)
    {
        var (database, _) = Over(Out("0123456789ab\n"), Out(printed));

        var group = database.DefaultAdminGroup(_project, Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, group.Error.Code);
        Assert.Contains("Admin group", group.Error.Message, StringComparison.Ordinal);
    }
}
