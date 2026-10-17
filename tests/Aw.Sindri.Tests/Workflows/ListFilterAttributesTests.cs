using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The typed list filters on the pinned dev server (API spec §3.8, review run 1 I3, I4, I9): a root instance of an incident
/// recipe and a child of a work recipe, each with the same declared input. The 'Urgency', 'Subject' and 'Root' attributes the
/// workflow sets, the filters that find each, the order, and the grammar of 'IS NULL' on the server (the filters match an
/// instance that never set an attribute as its default).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ListFilterAttributesTests : IDisposable
{
    /// <summary>The queries 'workflow list --urgency work' and '--root true' send, as the production code builds them (not copies of its text).</summary>
    private static readonly string[] _nullQueries =
    [
        "WorkflowType = 'ParentOfRecipe' AND " + InstanceFilters.ToQuery(new InstanceFilter(Urgency: RecipeUrgency.Work)),
        "WorkflowType = 'ParentOfRecipe' AND " + InstanceFilters.ToQuery(new InstanceFilter(Root: true)),
    ];

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task The_attributes_are_set_and_the_filters_find_the_root_and_the_child_by_urgency_subject_and_root()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.incident", WorkflowsHarness.Shipped("sindri/workflow.step.yaml")
            .Replace("id: workflow.step", "id: workflow.incident\nurgency: incident", StringComparison.Ordinal));
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        using var parents = new TemporalWorker(env.Client, new TemporalWorkerOptions(ParentOfRecipeWorkflow.TaskQueue).AddWorkflow<ParentOfRecipeWorkflow>());
        var sindri = served.Sindri;

        await parents.ExecuteAsync(async () =>
        {
            var rootStart = await WithSpec(WorkflowsHarness.RecipeStart(sindri, "workflow.incident", "T-1", ct));
            var childStart = await WithSpec(WorkflowsHarness.RecipeStart(sindri, "workflow.step", "T-2", ct));
            var root = await WorkflowsHarness.Start(env.Client, rootStart);
            var parent = await env.Client.StartWorkflowAsync((ParentOfRecipeWorkflow w) => w.RunAsync(childStart), new WorkflowOptions("p4b-parent", ParentOfRecipeWorkflow.TaskQueue));
            var child = env.Client.GetWorkflowHandle<RecipeWorkflow>(childStart.WorkflowId().Value);
            _ = await WorkflowsHarness.Until(root, static v => v.Task is not null);
            _ = await WorkflowsHarness.Until(child, static v => v.Task is not null);

            var rootAttributes = (await root.DescribeAsync()).TypedSearchAttributes;
            var childAttributes = (await child.DescribeAsync()).TypedSearchAttributes;
            Assert.Equal(("incident", "fact:spec", true), (rootAttributes.Get(SindriTemporal.UrgencyKey), rootAttributes.Get(SindriTemporal.SubjectKey), rootAttributes.Get(SindriTemporal.RootKey)));
            Assert.Equal(("work", "fact:spec", false), (childAttributes.Get(SindriTemporal.UrgencyKey), childAttributes.Get(SindriTemporal.SubjectKey), childAttributes.Get(SindriTemporal.RootKey)));

            Assert.Equal(["T-1/incident/1", "T-2/step/1"], await Addresses(sindri, 2, ["list"], ct));   // incident before work
            Assert.Equal(["T-1/incident/1"], await Addresses(sindri, 1, ["list", "--urgency", "incident"], ct));
            Assert.Equal(["T-2/step/1"], await Addresses(sindri, 1, ["list", "--urgency", "work"], ct));
            Assert.Equal(["T-1/incident/1"], await Addresses(sindri, 1, ["list", "--root", "true"], ct));
            Assert.Equal(["T-2/step/1"], await Addresses(sindri, 1, ["list", "--root", "false"], ct));
            Assert.Equal(["T-1/incident/1"], await Addresses(sindri, 1, ["list", "--recipe", "workflow.incident"], ct));
            Assert.Equal(["T-1/incident/1", "T-2/step/1"], await Addresses(sindri, 2, ["list", "--subject", "fact:spec"], ct));   // the subject key
            Assert.Equal(["T-1/incident/1", "T-2/step/1"], await Addresses(sindri, 2, ["list", "--subject", "spec.m"], ct));      // part of the subject label
            Assert.Empty(await Addresses(sindri, 0, ["list", "--subject", "nothing like it"], ct));

            await parent.TerminateAsync("test over");
            await root.TerminateAsync("test over");
        }, ct);
    }

    /// <summary>
    /// 'IS NULL' is in the dev server's visibility grammar and matches an execution that never set the attribute: the
    /// parent here is not a RecipeWorkflow and sets none of Sindri's attributes. This is what lets 'Urgency = work' and
    /// 'Root = true' find an instance started before those attributes existed.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task The_server_matches_an_execution_without_the_attribute_through_IS_NULL()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        using var worker = new TemporalWorker(env.Client, new TemporalWorkerOptions(ParentOfRecipeWorkflow.TaskQueue).AddWorkflow<ParentOfRecipeWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var parent = await env.Client.StartWorkflowAsync(
                (ParentOfRecipeWorkflow w) => w.RunAsync(null!), new WorkflowOptions("p4b-bare", ParentOfRecipeWorkflow.TaskQueue));   // why: the run isn't needed, only an execution with no Sindri attributes
            var found = new List<string>();
            foreach (var query in _nullQueries)
            {
                var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
                var ids = new List<string>();
                while (ids.Count == 0 && RealTime.NowDateTime < deadline)
                {
                    await foreach (var execution in env.Client.ListWorkflowsAsync(query).WithCancellation(ct))
                    {
                        ids.Add(execution.Id);
                    }

                    await Task.Delay(200, ct);
                }

                found.AddRange(ids);
            }

            Assert.Equal(["p4b-bare", "p4b-bare"], found);
            await parent.TerminateAsync("test over");
        }, ct);
    }

    private static async Task<InstanceStart> WithSpec(Task<InstanceStart> start)
    {
        var started = await start;
        var spec = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);
        return started with { Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("spec.md", spec) } };
    }

    /// <summary>The addresses 'workflow list <paramref name="args"/>' prints, one per line, retried until there are <paramref name="count"/> of them (visibility is eventually consistent).</summary>
    private static async Task<string[]> Addresses(IServiceProvider sindri, int count, string[] args, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        string[] last = [];
        while (RealTime.NowDateTime < deadline && last.Length != count)
        {
            var (_, stdout, _) = await WorkflowsHarness.Cli(sindri, args);
            last = [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(static l => !l.StartsWith("no instances", StringComparison.Ordinal)).Select(static l => l.Split("  ")[0])];
            await Task.Delay(count == 0 ? 0 : 200, ct);
        }

        return last;
    }
}
