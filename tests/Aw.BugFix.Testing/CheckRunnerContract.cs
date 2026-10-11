using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="ICheckRunner"/> contract, run against the scripted fake and the process runner.</summary>
public abstract class CheckRunnerContract
{
    /// <summary>Gets the runner under test.</summary>
    /// <summary>A digest key (hex) for the runs.</summary>
    protected const string Key = "000102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f";

    protected abstract ICheckRunner Runner { get; }

    /// <summary>Gets a working directory.</summary>
    protected abstract string Directory { get; }

    /// <summary>The exit code is the command's: 0 passes, non-zero fails.</summary>
    [Fact]
    public void The_exit_code_is_the_commands()
    {
        Expect.That(Runner.Run(["true"], Directory, $"{Directory}/true.log", Key) is { Code: 0, Ran: true }, "Equal(0, Runner.Run(['true'], Directory, $'{Directory}/true.log', Key))");
        Expect.That(Runner.Run(["false"], Directory, $"{Directory}/false.log", Key) is { Ran: true, Code: not 0 }, "NotEqual(0, Runner.Run(['false'], Directory, $'{Directory}/false.log', Key))");
    }

    /// <summary>A command that can't start fails; it never passes.</summary>
    [Fact]
    public void A_command_that_cannot_start_fails() =>
        Expect.That(Runner.Run(["no-such-program-aw-bugfix"], Directory, $"{Directory}/missing.log", Key) is { Ran: false, Outcome: RunOutcome.Broken }, "NotEqual(0, Runner.Run(['no-such-program-aw-bugfix'], Directory, $'{Directory}/missing.log', Key))");

    /// <summary>The same program resolves to the same file each run, with a recorded hash; a program that doesn't exist has none.</summary>
    [Fact]
    public void A_run_names_the_program_file_it_ran_and_its_environment()
    {
        var first = Runner.Run(["true"], Directory, $"{Directory}/a.log", Key);
        var second = Runner.Run(["true"], Directory, $"{Directory}/b.log", Key);
        var missing = Runner.Run(["no-such-program-aw-bugfix"], Directory, $"{Directory}/c.log", Key);

        Expect.That(first.ProgramSha256.Length > 0 && first.ProgramSha256 == second.ProgramSha256, "the program's hash is recorded and stable");
        Expect.That(first.Environment.ContainsKey("PATH") && first.Environment.SequenceEqual(second.Environment), "the environment (PATH, …) is recorded and stable");
        Expect.That(missing is { Ran: false, ProgramSha256: "" }, "a program that doesn't resolve didn't run and has no hash");
    }

    /// <summary>A command that runs past the time limit is cut off and didn't run to a verdict.</summary>
    [Fact]
    public void A_command_past_the_time_limit_did_not_run() =>
        Expect.That(HangingRunner.Run(HangingCommand, Directory, $"{Directory}/hang.log", Key) is { Ran: false, Outcome: RunOutcome.Broken }, "a run past the limit is broken");

    /// <summary>Gets a runner whose time limit <see cref="HangingCommand"/> exceeds.</summary>
    protected abstract ICheckRunner HangingRunner { get; }

    /// <summary>Gets a command that runs longer than <see cref="HangingRunner"/>'s limit.</summary>
    protected abstract IReadOnlyList<string> HangingCommand { get; }
}
