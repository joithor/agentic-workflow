using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// 'sindri workflow complete' on the desk runtime (engine spec rev 8.1 §5.2, §5.3, §6.3; "Phase 4" signals 2–4) against
/// the pinned dev server: the report is the 'complete' Update with the nonce as its id, validated synchronously, and a
/// refused report records nothing.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CompleteStepTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Complete_moves_produce_to_checks_then_passed_and_accepted_end_done_resolved()
    {
        await Run("T-1", async desk =>
        {
            var output = _harness.File("out.md", "the output");

            var done = await desk.Complete("--run", "1.1", "--outcome", "done", "--output", "fact:output=" + output, "--json");
            var passed = await desk.Complete("--run", "2.1", "--outcome", "passed");
            var accepted = await desk.Complete("--run", "3.1", "--outcome", "accepted");

            Assert.Equal((0, string.Empty), (done.Exit, done.Stderr));
            using var json = JsonDocument.Parse(done.Stdout);
            Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("nonce").GetString());
            Assert.Equal("-> checks", json.RootElement.GetProperty("result").GetProperty("happened").GetString());
            Assert.Equal((0, string.Empty), (passed.Exit, passed.Stderr));
            Assert.Equal(["  -> verify"], passed.Lines[1..]);
            Assert.Equal((0, string.Empty), (accepted.Exit, accepted.Stderr));
            Assert.Matches(Header("T-1/step/1", "done: resolved"), accepted.Lines[0]);
            Assert.Equal(["  -> done"], accepted.Lines[1..]);
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition", "workflow.transition"], await desk.Events());
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task The_same_complete_with_the_same_nonce_is_already_applied_and_records_nothing_new()
    {
        await Run("T-2", async desk =>
        {
            string[] report = ["--run", "1.1", "--outcome", "done", "--output", "fact:output=" + _harness.File("out.md", "the output"), "--nonce", new string('b', 32)];
            var first = await desk.Complete(report);
            var events = await desk.Events();

            var again = await desk.Complete(report);

            Assert.Equal((0, string.Empty), (first.Exit, first.Stderr));
            Assert.Matches(Header("T-2/step/1", "checks: desk task"), first.Lines[0]);
            Assert.Equal(["  -> checks"], first.Lines[1..]);
            Assert.Equal((0, string.Empty), (again.Exit, again.Stderr));
            Assert.Equal(["  already applied (-> checks)"], again.Lines[1..]);
            Assert.Equal(events, await desk.Events());
            Assert.Equal(["workflow.started", "workflow.transition"], events);
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task Stale_token_report_records_nothing()
    {
        await Run("T-3", async desk =>
        {
            var stale = await desk.Complete("--run", "2.1", "--outcome", "done", "--output", "fact:output=" + _harness.File("out.md", "the output"));

            Assert.Equal(2, stale.Exit);
            Assert.StartsWith("SND-WF-005 ", stale.Stderr, StringComparison.Ordinal);
            Assert.Equal(["workflow.started"], await desk.Events());
            var view = await desk.Until(static v => v.Task is not null);
            Assert.Equal(("produce", "1.1"), (view.State, view.Run?.Value));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task Malformed_report_is_rejected_without_a_round()
    {
        await Run("T-4", async desk =>
        {
            var nonce = new string('c', 32);
            var output = _harness.File("out.md", "the output");

            var undeclared = await desk.Complete("--run", "1.1", "--outcome", "passed", "--output", "fact:output=" + output, "--nonce", nonce);
            var missing = await desk.Complete("--run", "1.1", "--outcome", "done", "--nonce", nonce);
            var unchanged = await desk.Until(static v => v.Task is not null);
            var events = await desk.Events();
            var valid = await desk.Complete("--run", "1.1", "--outcome", "done", "--output", "fact:output=" + output, "--nonce", nonce);

            Assert.Equal(2, undeclared.Exit);
            Assert.StartsWith("SND-WF-008 ", undeclared.Stderr, StringComparison.Ordinal);
            Assert.Contains("'passed'", undeclared.Stderr, StringComparison.Ordinal);
            Assert.Equal(2, missing.Exit);
            Assert.StartsWith("SND-WF-008 ", missing.Stderr, StringComparison.Ordinal);
            Assert.Contains("fact:output", missing.Stderr, StringComparison.Ordinal);
            Assert.Equal(("produce", "1.1"), (unchanged.State, unchanged.Run?.Value));
            Assert.Equal(["workflow.started"], events);
            Assert.Equal((0, string.Empty), (valid.Exit, valid.Stderr));
            Assert.Equal(["  -> checks"], valid.Lines[1..]);
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_used_nonce_on_another_run_is_SND_WF_009_and_records_nothing()
    {
        await Run("T-5", async desk =>
        {
            var nonce = new string('d', 32);
            var first = await desk.Complete("--run", "1.1", "--outcome", "done", "--output", "fact:output=" + _harness.File("out.md", "the output"), "--nonce", nonce);
            var events = await desk.Events();

            var reused = await desk.Complete("--run", "2.1", "--outcome", "passed", "--nonce", nonce);

            Assert.Equal(0, first.Exit);
            Assert.Equal(2, reused.Exit);
            Assert.StartsWith("SND-WF-009 ", reused.Stderr, StringComparison.Ordinal);
            Assert.Equal(events, await desk.Events());
            var view = await desk.Until(static v => v.Task is not null);
            Assert.Equal(("checks", "2.1"), (view.State, view.Run?.Value));
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 120_000)]
    public async Task After_the_worker_restarts_the_open_desk_task_keeps_its_run_token_and_complete_works()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.StepStart(served.Sindri, "T-6", ct));
        var desk = new DeskInstance(served.Sindri, handle);
        _ = await desk.Until(static v => v.Task is not null);

        await served.RestartWorker();
        try
        {
            var view = await desk.Until(static v => v.Task is not null);
            var done = await desk.Complete("--run", "1.1", "--outcome", "done", "--output", "fact:output=" + _harness.File("out.md", "the output"));

            Assert.Equal(("produce", "1.1"), (view.State, view.Run?.Value));
            Assert.Equal((0, string.Empty), (done.Exit, done.Stderr));
            Assert.Equal(["  -> checks"], done.Lines[1..]);
        }
        finally
        {
            await handle.TerminateAsync("test over");
        }
    }

    /// <summary>The status header (spec §6.4) of <paramref name="address"/> in <paramref name="state"/>, as a pattern.</summary>
    private static string Header(string address, string state) =>
        $@"^{Regex.Escape(address)}  workflow\.step@[0-9a-f]{{12}}  v\d+  {Regex.Escape(state)}  since \d\d:\d\d \(.+\)$";

    /// <summary>Starts 'workflow.step' on <paramref name="item"/> under the serve worker, waits for its first desk task, runs <paramref name="body"/>, then ends it.</summary>
    private async Task Run(string item, Func<DeskInstance, Task> body, CancellationToken ct)
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, ct);
        var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.StepStart(served.Sindri, item, ct));
        try
        {
            var desk = new DeskInstance(served.Sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);
            await body(desk);
        }
        finally
        {
            if ((await handle.DescribeAsync()).Status == Temporalio.Api.Enums.V1.WorkflowExecutionStatus.Running)
            {
                await handle.TerminateAsync("test over");
            }
        }
    }
}
