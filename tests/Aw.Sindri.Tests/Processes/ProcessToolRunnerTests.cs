using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Processes;

// The real runner, over harmless system tools (printf, sh); never security or op.
[Collection(ProcessEnvironment.Name)]
public sealed class ProcessToolRunnerTests
{
    private readonly ProcessToolRunner _runner = new();

    [Fact, Trait("Lane", "integration")]
    public void It_returns_the_exit_code_and_standard_output()
    {
        var run = _runner.Run("printf", ["%s|%s", "a b", "$HOME"], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new ToolRun(ToolOutcome.Exited, 0, "a b|$HOME"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_non_zero_exit_is_reported()
        => Assert.Equal(3, _runner.Run("sh", ["-c", "exit 3"], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken).ExitCode);

    [Fact, Trait("Lane", "integration")]
    public void A_tool_not_on_PATH_is_missing()
        => Assert.Equal(ToolRun.Missing, _runner.Run("aw-no-such-tool-7f3c2a", [], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken));

    [Fact, Trait("Lane", "integration")]
    public void A_tool_past_its_timeout_is_killed()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var run = _runner.Run("sleep", ["10"], TimeSpan.FromMilliseconds(200), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ToolRun.TimedOut, run);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact, Trait("Lane", "integration")]
    public void An_unset_variable_is_absent_in_the_child_and_a_set_one_is_present()
    {
        Environment.SetEnvironmentVariable("AW_TEST_INHERITED", "leaks");
        try
        {
            var options = new ToolRunOptions(Environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_TEST_INHERITED"] = null, ["AW_TEST_ADDED"] = "added" });

            var run = _runner.Run("sh", ["-c", "printf '%s|%s' \"${AW_TEST_INHERITED-absent}\" \"${AW_TEST_ADDED-absent}\""], TimeSpan.FromSeconds(5), options: options, cancellationToken: TestContext.Current.CancellationToken);
            var untouched = _runner.Run("sh", ["-c", "printf '%s' \"${AW_TEST_INHERITED-absent}\""], TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("absent|added", run.Output);
            Assert.Equal("leaks", untouched.Output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AW_TEST_INHERITED", value: null);
        }
    }
}
