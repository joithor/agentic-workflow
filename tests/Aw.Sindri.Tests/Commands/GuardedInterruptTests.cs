using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>What the CLI does with a cancellation: a Ctrl-C ends the verb as 'interrupted' with exit 130; any other cancellation is a bug and surfaces.</summary>
public sealed class GuardedInterruptTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_cancellation_after_a_Ctrl_C_prints_interrupted_and_exits_130()
    {
        using var interrupt = Interrupt.For(["status"]);
        var stderr = new StringWriter();

        var exit = await CommandTable.Guarded(interrupt, stderr, i =>
        {
            i.Raise();
            throw new OperationCanceledException(i.Token);
        });

        Assert.Equal(130, exit);
        Assert.Equal("interrupted", stderr.ToString().Trim());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancellation_with_no_Ctrl_C_is_not_swallowed()
    {
        using var interrupt = Interrupt.For(["status"]);
        var stderr = new StringWriter();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => CommandTable.Guarded(interrupt, stderr, _ => throw new OperationCanceledException()));

        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_verb_that_ends_normally_keeps_its_exit_code()
    {
        using var interrupt = Interrupt.For(["status"]);

        var exit = await CommandTable.Guarded(interrupt, new StringWriter(), _ => Task.FromResult(2));

        Assert.Equal(2, exit);
    }
}
