using Aw.Kernel.Testing;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ICommandRunner contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class CommandRunnerContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The runner under test.</summary>
    /// <returns>The runner.</returns>
    protected abstract ICommandRunner Create();

    /// <summary>An argument vector that prints the text and exits 0.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The argument vector.</returns>
    protected abstract IReadOnlyList<string> Prints(string text);

    /// <summary>An argument vector that exits with the code.</summary>
    /// <param name="code">The exit code.</param>
    /// <returns>The argument vector.</returns>
    protected abstract IReadOnlyList<string> Exits(int code);

    /// <summary>An argument vector that runs far longer than any test timeout.</summary>
    /// <returns>The argument vector.</returns>
    protected abstract IReadOnlyList<string> Hangs();

    /// <summary>An argument vector naming a program that does not exist.</summary>
    /// <returns>The argument vector.</returns>
    protected abstract IReadOnlyList<string> Missing();

    private static CommandRequest Request(IReadOnlyList<string> argv, TimeSpan? timeout = null) => new(argv, timeout ?? TimeSpan.FromSeconds(20));

    /// <summary>A command's exit code and output come back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Output_and_exit_code_come_back()
    {
        var runner = Create();

        var ok = await runner.Run(Request(Prints("hello")), _none);
        var bad = await runner.Run(Request(Exits(3)), _none);

        Expect.That(ok.IsSuccess && ok.Value.ExitCode == 0 && ok.Value.Stdout.Contains("hello", StringComparison.Ordinal) && !ok.Value.TimedOut, "exit 0 with stdout");
        Expect.That(bad.IsSuccess && bad.Value.ExitCode == 3, "a non-zero exit is a result, not an error");
    }

    /// <summary>A command past its timeout is killed and flagged.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_command_past_its_timeout_is_killed()
    {
        var result = await Create().Run(Request(Hangs(), TimeSpan.FromMilliseconds(400)), _none);

        Expect.That(result.IsSuccess && result.Value.TimedOut, "TimedOut is set");
    }

    /// <summary>A program that does not exist is AW-META-009.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_program_is_an_error()
    {
        var result = await Create().Run(Request(Missing()), _none);

        Expect.That(result.IsError && result.Error.Code == MetaErrors.CommandNotStarted.Code, "AW-META-009");
    }

    /// <summary>With a log file the output is appended to it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Output_is_appended_to_the_log_file()
    {
        var log = Path.Combine(Path.GetTempPath(), "aw-meta-cmd-" + RealTime.NewGuid().ToString("N") + ".log");
        try
        {
            var result = await Create().Run(Request(Prints("to the log")) with { LogFile = log }, _none);

            var text = File.Exists(log) ? await File.ReadAllTextAsync(log, _none) : string.Empty;

            Expect.That(result.IsSuccess && text.Contains("to the log", StringComparison.Ordinal), "the log holds the output");
        }
        finally
        {
            File.Delete(log);
        }
    }
}
