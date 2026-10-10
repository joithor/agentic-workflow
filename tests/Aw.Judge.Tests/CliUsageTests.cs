namespace Aw.Judge.Tests;

// The frozen surface for a missing or unknown first word (TS cli.ts's default branch).
public sealed class CliUsageTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task No_command_prints_the_question_usage_and_exits_1()
    {
        var run = await new JudgeCli(_state).Run();

        Assert.Equal(new CliRun(1, "", "usage: judge <question> < input.json\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_first_word_is_an_unknown_question_whatever_stdin_holds()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "unknown question: no-such-question\n"), await cli.RunWithInput("", "no-such-question"));
        Assert.Equal(new CliRun(1, "", "unknown question: no-such-question\n"), await cli.RunWithInput("{}", "no-such-question"));
    }

    public void Dispose() => _state.Dispose();
}
