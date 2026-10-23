using Aw.Evidence.Core;
using Aw.Evidence.Core.Publishing;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IGhRunner contract: any exit code, standard output and standard error read back, and a gh that would not start is AW-EVIDENCE-015.</summary>
public abstract class GhRunnerContract
{
    /// <summary>Arranges a gh with this behavior.</summary>
    /// <param name="program">What gh does.</param>
    /// <returns>The runner.</returns>
    protected abstract Task<GhUnderTest> ArrangeAsync(GhProgram program);

    /// <summary>Arranges a gh that is not there.</summary>
    /// <returns>The runner.</returns>
    protected abstract Task<GhUnderTest> ArrangeMissingAsync();

    /// <summary>The exit code, standard output and standard error read back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_exit_code_and_both_streams_read_back()
    {
        var gh = await ArrangeAsync(new GhProgram(3, "hello", "careful"));

        var outcome = await gh.Runner.RunAsync(Call(), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value == new GhOutcome(3, "hello", "careful"), "exit code 3, stdout 'hello' and stderr 'careful' come back");
    }

    /// <summary>A non-zero exit is an outcome, not an error: the caller reads gh's message (a 404 on a missing branch is an answer).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failing_exit_is_an_outcome_not_an_error()
    {
        var gh = await ArrangeAsync(new GhProgram(1, string.Empty, "HTTP 404"));

        var outcome = await gh.Runner.RunAsync(Call(), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value.ExitCode == 1 && outcome.Value.Stderr == "HTTP 404", "exit 1 is an outcome with its stderr");
    }

    /// <summary>A gh that is not there is a run failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_gh_that_would_not_start_is_a_run_failure()
    {
        var gh = await ArrangeMissingAsync();

        var outcome = await gh.Runner.RunAsync(Call(), CancellationToken.None);

        Expect.That(outcome.IsError && outcome.Error.Code == EvidenceErrors.RunFailed.Code, "a gh that cannot start is AW-EVIDENCE-015");
    }

    private static GhCall Call() => new(["api", "repos/o/r/git/ref/heads/main"]);
}
