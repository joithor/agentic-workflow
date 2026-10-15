using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class ProcessCliRunnerTests : IDisposable
{
    private readonly TempDirectory _root = new();

    private static CliInvocation Invoke(string executable, string? stdin = null, params (string Name, string Value)[] environment) =>
        new(executable, [], stdin, environment.ToDictionary(static e => e.Name, static e => e.Value, StringComparer.Ordinal));

    private static ProcessCliRunner Runner(int seconds = 20) => new(TimeSpan.FromSeconds(seconds));

    [Fact]
    public async Task ApiRun_runs_each_cli_call_in_a_fresh_temp_directory()
    {
        var tool = StubCommand.Write(_root, "where", "printf '%s|%s|%s' \"$PWD\" \"$HOME\" \"$TMPDIR\"");

        var first = (await Runner().RunAsync(Invoke(tool), TestContext.Current.CancellationToken)).Value.Stdout.Split('|');
        var second = (await Runner().RunAsync(Invoke(tool), TestContext.Current.CancellationToken)).Value.Stdout.Split('|');

        Assert.Equal(first[0], first[1], StringComparer.Ordinal);
        Assert.Equal(first[0], first[2], StringComparer.Ordinal);
        Assert.NotEqual(first[0], second[0], StringComparer.Ordinal);
        Assert.False(Directory.Exists(first[0]));
        Assert.False(Directory.Exists(second[0]));
        Assert.NotEqual(_root.Path, first[0], StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_call_gets_exactly_the_environment_it_is_given_plus_PATH_HOME_and_TMPDIR()
    {
        var tool = StubCommand.Write(_root, "envs", "printf '%s|%s|%s|%s' \"${AW_EVIDENCE_TEST_LEAK-unset}\" \"${NOTES_REGION-unset}\" \"${LANG-unset}\" \"${PATH+path}\"");
        Environment.SetEnvironmentVariable("AW_EVIDENCE_TEST_LEAK", "leaked");
        try
        {
            var outcome = await Runner().RunAsync(Invoke(tool, stdin: null, ("NOTES_REGION", "north"), ("LANG", "C")), TestContext.Current.CancellationToken);

            Assert.Equal("unset|north|C|path", outcome.Value.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AW_EVIDENCE_TEST_LEAK", value: null);
        }
    }

    [Fact]
    public async Task Standard_input_reaches_the_program()
    {
        var tool = StubCommand.Write(_root, "echo-in", "cat");

        var outcome = await Runner().RunAsync(Invoke(tool, stdin: "from the script"), TestContext.Current.CancellationToken);

        Assert.Equal("from the script", outcome.Value.Stdout);
    }

    [Fact]
    public async Task A_bare_program_name_is_refused_so_nothing_is_found_through_PATH()
    {
        var outcome = await Runner().RunAsync(Invoke("sh"), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RunFailed.Code, outcome.Error.Code);
        Assert.Contains("absolute", outcome.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_program_that_runs_past_the_limit_is_killed_and_reported()
    {
        var tool = StubCommand.Write(_root, "slow", "sleep 30");

        var outcome = await Runner(seconds: 1).RunAsync(Invoke(tool), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RunFailed.Code, outcome.Error.Code);
        Assert.Contains("did not finish", outcome.Error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();
}
