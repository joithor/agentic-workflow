using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Children on the time-skipping server (engine P7 U2, workflow engine spec §5.8): a child wait starts the child as a Temporal child
/// workflow at its address; its park, resume and escalation reach the parent and its end comes back as the wait's satisfaction; a parent's
/// cancel reaches its live children; a looped wait starts a new child per entry; a grandchild starts under its child; every history replays clean.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ChildWorkflowTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_child_wait_starts_the_child_at_its_address_and_the_parent_follows_its_end()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-71");
            var (view, address) = await ChildTree.OneChild(parent);
            var child = tree.At(address);
            var waiting = await WorkflowsHarness.Until(child, static v => v.State == "waiting");
            await ChildTree.Go(child);
            var result = await parent.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            Assert.Equal(ChildTree.ChildOf(parent.Id, "workflow.t-child", 1), address);
            Assert.Equal("run", view.State);
            _ = Assert.NotNull(waiting.ParentId);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
            Assert.Null((await InterpreterEvalTests.Replay(parent, TestContext.Current.CancellationToken)).ReplayFailure);
            Assert.Null((await InterpreterEvalTests.Replay(child, TestContext.Current.CancellationToken)).ReplayFailure);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_child_park_mirrors_on_the_parent_with_propagate()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-72");
            var (_, address) = await ChildTree.OneChild(parent);
            _ = await WorkflowsHarness.Until(tree.At(address), static v => v.State == "waiting");
            await tree.PastTheHour();
            var mirrored = await WorkflowsHarness.Until(parent, static v => v.State == "parked");

            Assert.Equal(ParkReason.Timeout, mirrored.Question?.Reason);
            Assert.Equal(address, Assert.Single(mirrored.Children).Address.Value);
            await parent.TerminateAsync("test over").WaitAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_child_resume_pops_the_parents_mirror()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-73");
            var (_, address) = await ChildTree.OneChild(parent);
            var child = tree.At(address);
            _ = await WorkflowsHarness.Until(child, static v => v.State == "waiting");
            await tree.PastTheHour();
            _ = await WorkflowsHarness.Until(parent, static v => v.State == "parked");
            _ = await ChildTree.Resume(child);
            var released = await WorkflowsHarness.Until(parent, static v => v.State == "run" && v.Question is null);

            Assert.Equal(address, Assert.Single(released.Children).Address.Value);
            await parent.TerminateAsync("test over").WaitAsync(TestContext.Current.CancellationToken);
            await child.TerminateAsync("test over").WaitAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_child_escalation_escalates_the_parent_as_child()
    {
        await Run([ChildRecipes.Child("escalate"), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-74");
            var (_, address) = await ChildTree.OneChild(parent);
            _ = await WorkflowsHarness.Until(tree.At(address), static v => v.State == "waiting");
            await tree.PastTheHour();
            var escalated = await WorkflowsHarness.Until(parent, static v => v.State == "escalated");

            Assert.Equal(SuspensionKind.Escalation, escalated.Question?.Kind);
            Assert.Contains(address, escalated.Next, StringComparison.Ordinal);
            await parent.TerminateAsync("test over").WaitAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_child_that_ends_while_mirrored_releases_its_parent()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-75");
            var (_, address) = await ChildTree.OneChild(parent);
            var child = tree.At(address);
            _ = await WorkflowsHarness.Until(child, static v => v.State == "waiting");
            await tree.PastTheHour();
            _ = await WorkflowsHarness.Until(parent, static v => v.State == "parked");
            _ = await ChildTree.Cancel(child);
            var result = await parent.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            Assert.Equal(("dropped", "unresolved"), (result.State, result.Outcome));
            Assert.Null((await InterpreterEvalTests.Replay(parent, TestContext.Current.CancellationToken)).ReplayFailure);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_parent_cancel_cancels_every_live_child()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-76");
            var (_, address) = await ChildTree.OneChild(parent);
            var child = tree.At(address);
            _ = await WorkflowsHarness.Until(child, static v => v.State == "waiting");
            _ = await ChildTree.Cancel(parent);
            var cancelled = await child.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            var description = await child.DescribeAsync();

            Assert.Equal(("cancelled", "cancelled"), (cancelled.State, cancelled.Outcome));
            Assert.Equal(WorkflowExecutionStatus.Completed, description.Status);   // its own cancelled, not the parent-close terminate
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_looped_child_wait_starts_a_new_child_per_entry()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Looping()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-77");
            var (_, first) = await ChildTree.OneChild(parent);
            await ChildTree.Go(tree.At(first));
            var second = (await WorkflowsHarness.Until(parent, v => v.Children.Count == 1 && v.Children[0].Address.Value != first)).Children[0].Address.Value;
            await ChildTree.Go(tree.At(second));
            var result = await parent.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            Assert.Equal(ChildTree.ChildOf(parent.Id, "workflow.t-child", 1), first);
            Assert.EndsWith(".2", second, StringComparison.Ordinal);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task A_grandchild_starts_under_its_child()
    {
        await Run([ChildRecipes.Child(), ChildRecipes.Mid(), ChildRecipes.Top()], async tree =>
        {
            var top = await tree.Start("workflow.t-top", "T-78");
            var (_, mid) = await ChildTree.OneChild(top);
            var (_, grandchild) = await ChildTree.OneChild(tree.At(mid));
            await ChildTree.Go(tree.At(grandchild));
            var result = await top.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            Assert.Equal(ChildTree.ChildOf(mid, "workflow.t-child", 1), grandchild);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task The_parents_status_names_its_children_and_answer_child()
    {
        await Run([ChildRecipes.Child("escalate"), ChildRecipes.Parent()], async tree =>
        {
            var parent = await tree.Start("workflow.t-parent", "T-79");
            var (running, address) = await ChildTree.OneChild(parent);
            _ = await WorkflowsHarness.Until(tree.At(address), static v => v.State == "waiting");
            await tree.PastTheHour();
            var escalated = await WorkflowsHarness.Until(parent, static v => v.State == "escalated");

            Assert.Equal(address, Assert.Single(running.Children).Address.Value);
            Assert.Equal(("child", address), (escalated.AnswerLine?.Via, escalated.AnswerLine?.Child));   // 'answer.child' reads the child's address, not "the child"
            await parent.TerminateAsync("test over").WaitAsync(TestContext.Current.CancellationToken);
        });
    }

    /// <summary>Runs <paramref name="body"/> against the approved <paramref name="recipes"/> on a time-skipping server of its own: a test that skips time must not move the clock under another.</summary>
    private async Task Run(string[] recipes, Func<ChildTree, Task> body)
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var yaml in recipes)
        {
            _ = _harness.Approve(yaml);
        }

        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(() => body(new ChildTree(sindri, server.Environment, _harness)), ct);
    }
}
