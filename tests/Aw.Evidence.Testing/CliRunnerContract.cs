using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The ICliRunner contract: any exit code and standard output read back, and a program that would not start is AW-EVIDENCE-015.</summary>
public abstract class CliRunnerContract
{
    /// <summary>Arranges a program with this behavior and returns a runner and the program's path.</summary>
    /// <param name="program">What the program does.</param>
    /// <returns>The runner and the path.</returns>
    protected abstract Task<CliUnderTest> ArrangeAsync(CliProgram program);

    /// <summary>Arranges a path no program is at.</summary>
    /// <returns>The runner and the missing path.</returns>
    protected abstract Task<CliUnderTest> ArrangeMissingAsync();

    /// <summary>The exit code and standard output read back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_exit_code_and_stdout_read_back()
    {
        var cli = await ArrangeAsync(new CliProgram(3, "hello"));

        var outcome = await cli.Runner.RunAsync(Invocation(cli.Executable), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value.ExitCode == 3 && outcome.Value.Stdout == "hello", "exit code 3 and stdout 'hello' come back");
    }

    /// <summary>A non-zero exit is an outcome, not an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failing_exit_is_an_outcome_not_an_error()
    {
        var cli = await ArrangeAsync(new CliProgram(1, string.Empty));

        var outcome = await cli.Runner.RunAsync(Invocation(cli.Executable), CancellationToken.None);

        Expect.That(outcome.IsSuccess && outcome.Value.ExitCode == 1, "exit 1 is an outcome");
    }

    /// <summary>A program that is not there is a run failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_program_that_would_not_start_is_a_run_failure()
    {
        var cli = await ArrangeMissingAsync();

        var outcome = await cli.Runner.RunAsync(Invocation(cli.Executable), CancellationToken.None);

        Expect.That(outcome.IsError && outcome.Error.Code == EvidenceErrors.RunFailed.Code, "a program that cannot start is AW-EVIDENCE-015");
    }

    private static CliInvocation Invocation(string executable) =>
        new(executable, [], Stdin: null, new Dictionary<string, string>(StringComparer.Ordinal));
}
