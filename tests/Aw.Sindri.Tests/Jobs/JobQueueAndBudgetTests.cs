using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The bounds on a job's progress (API P6 unit C, Q3): the line queue and the event budget.</summary>
[Lane(TestLane.Unit)]
public sealed class JobQueueAndBudgetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<string>> Read(JobLineQueue queue)
    {
        var lines = new List<string>();
        await foreach (var line in queue.ReadAll(Token))
        {
            lines.Add(line);
        }

        return lines;
    }

    [Fact]
    public async Task A_full_line_queue_drops_the_oldest_and_says_so()
    {
        var queue = new JobLineQueue(JobLimits.QueueLines);
        for (var n = 1; n <= 300; n++)
        {
            queue.Offer(string.Create(CultureInfo.InvariantCulture, $"line {n}"));
        }

        queue.Complete();
        var lines = await Read(queue);

        Expect.Equal(JobLimits.QueueLines + 1, lines.Count);
        Expect.Equal("(44 lines dropped)", lines[0]);
        Expect.Equal("line 45", lines[1]);
        Expect.Equal("line 300", lines[^1]);
    }

    [Fact]
    public async Task A_queue_that_never_filled_drops_nothing()
    {
        var queue = new JobLineQueue(JobLimits.QueueLines);
        queue.Offer("a");
        queue.Offer("b");
        queue.Complete();

        Expect.Sequence(["a", "b"], await Read(queue));
    }

    [Fact]
    public async Task A_reader_waiting_on_an_empty_queue_gets_a_line_offered_later_and_stops_when_it_completes()
    {
        var queue = new JobLineQueue(4);
        var reading = Read(queue);

        queue.Offer("late");
        queue.Complete();

        Expect.Sequence(["late"], await reading);
    }

    [Fact]
    public void Progress_stops_at_2000_events()
    {
        var budget = new JobProgressBudget();
        var decisions = Enumerable.Range(1, 2_005).Select(_ => budget.Admit()).ToList();

        Expect.Equal(JobLimits.MaxProgressEvents, decisions.Count(d => d == ProgressDecision.Write));
        Expect.Equal(ProgressDecision.Truncate, decisions[JobLimits.MaxProgressEvents]);
        Expect.Equal(1, decisions.Count(d => d == ProgressDecision.Truncate));
        Expect.Equal(4, decisions.Count(d => d == ProgressDecision.Drop));
        Expect.Equal(JobLimits.MaxProgressEvents + 1, budget.Written);
    }
}
