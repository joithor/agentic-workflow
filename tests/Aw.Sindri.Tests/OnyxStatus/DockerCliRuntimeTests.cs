using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The adapter over a scripted tool runner: the commands it runs and how it reads what they print.
public sealed class DockerCliRuntimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Ok(string output) => new(ToolOutcome.Exited, 0, output + "\n");

    private static ToolRun Fails { get; } = new(ToolOutcome.Exited, 1, "");

    private static FakeToolRunner Docker(ToolRun version, ToolRun compose, ToolRun info) => new((_, arguments) => arguments[0] switch
    {
        "version" => version,
        "compose" => compose,
        _ => info,
    });

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_missing_cli()
    {
        var facts = new DockerCliRuntime(new FakeToolRunner(ToolRun.Missing)).Inspect(Token);

        Assert.False(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.MemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_daemon_down_when_server_version_fails()
    {
        // Docker installed, Docker Desktop not started: `docker version` prints the client and fails on the server.
        var facts = new DockerCliRuntime(Docker(Fails, Ok("v2.40.3"), Fails)).Inspect(Token);

        Assert.True(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.Null(facts.ServerVersion);
        Assert.True(facts.ComposeV2);
        Assert.Null(facts.MemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_treats_a_timeout_as_daemon_down()
    {
        // A daemon that is starting up and hangs: the 5 s timeout kills the call.
        var facts = new DockerCliRuntime(Docker(ToolRun.TimedOut, ToolRun.TimedOut, ToolRun.TimedOut)).Inspect(Token);

        Assert.True(facts.CliFound);
        Assert.False(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_compose_v1_only()
    {
        // Only legacy docker-compose (v1): `docker compose version` fails.
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0"), Fails, Ok("16818978816"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.False(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_memory_bytes()
    {
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0"), Ok("v2.40.3"), Ok("16818978816"))).Inspect(Token);

        Assert.Equal(16_818_978_816, facts.MemoryBytes);
        Assert.Equal("29.4.0", facts.ServerVersion);
        Assert.Equal("v2.40.3", facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reports_a_version_that_is_not_plain_text_as_unreadable_with_the_fact_still_true()
    {
        var facts = new DockerCliRuntime(Docker(Ok("29.4.0\u001b[2J\u001b]0;pwned\a"), Ok("v2.40.3 \u001b[31m"), Ok("16818978816"))).Inspect(Token);

        Assert.True(facts.DaemonAnswers);
        Assert.Null(facts.ServerVersion);
        Assert.True(facts.ComposeV2);
        Assert.Null(facts.ComposeVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_reads_the_data_root()
    {
        var facts = new DockerCliRuntime(DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker")).Inspect(Token);

        Assert.Equal("/var/lib/docker", facts.DataRoot);
        Assert.Equal(16_818_978_816, facts.MemoryBytes);
    }

    private static FakeToolRunner DockerWithInfo(string version, string memory, string dataRoot) => new((_, arguments) => arguments[0] switch
    {
        "version" => Ok(version),
        "compose" => Ok("v2.40.3"),
        _ => arguments[2] == "{{.DockerRootDir}}" ? Ok(dataRoot) : Ok(memory),
    });

    [Fact, Trait("Lane", "unit")]
    public void Inspect_runs_exactly_the_four_commands_with_a_five_second_timeout_each()
    {
        var runner = DockerWithInfo("29.4.0", "16818978816", "/var/lib/docker");

        _ = new DockerCliRuntime(runner).Inspect(Token);

        Assert.Equal(
            [
                ["version", "--format", "{{.Server.Version}}"],
                ["compose", "version", "--short"],
                ["info", "--format", "{{.MemTotal}}"],
                ["info", "--format", "{{.DockerRootDir}}"],
            ],
            runner.Calls.Select(static c => c.Arguments));
        Assert.All(runner.Calls, static c => Assert.Equal("docker", c.FileName));
        Assert.All(runner.Calls, static c => Assert.Equal(TimeSpan.FromSeconds(5), c.Timeout));
    }

    [Fact, Trait("Lane", "unit")]
    public void Inspect_does_not_ask_info_of_a_daemon_that_does_not_answer()
    {
        var runner = Docker(Fails, Ok("v2.40.3"), Ok("16818978816"));

        _ = new DockerCliRuntime(runner).Inspect(Token);

        Assert.DoesNotContain(runner.Calls, static c => c.Arguments[0] == "info");
    }
}
