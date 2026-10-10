using Aw.BugFix.Core.Ports;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="ICheckRunner"/> contract, run against the scripted fake and the process runner.</summary>
public abstract class CheckRunnerContract
{
    /// <summary>Gets the runner under test.</summary>
    protected abstract ICheckRunner Runner { get; }

    /// <summary>Gets a working directory.</summary>
    protected abstract string Directory { get; }

    /// <summary>The exit code is the command's: 0 passes, non-zero fails.</summary>
    [Fact]
    public void The_exit_code_is_the_commands()
    {
        Expect.Equal(0, Runner.Run(["true"], Directory, $"{Directory}/true.log"), "Equal(0, Runner.Run(['true'], Directory, $'{Directory}/true.log'))");
        Expect.That(!Equals(0, Runner.Run(["false"], Directory, $"{Directory}/false.log")), "NotEqual(0, Runner.Run(['false'], Directory, $'{Directory}/false.log'))");
    }

    /// <summary>A command that can't start fails; it never passes.</summary>
    [Fact]
    public void A_command_that_cannot_start_fails() =>
        Expect.That(!Equals(0, Runner.Run(["no-such-program-aw-bugfix"], Directory, $"{Directory}/missing.log")), "NotEqual(0, Runner.Run(['no-such-program-aw-bugfix'], Directory, $'{Directory}/missing.log'))");
}
