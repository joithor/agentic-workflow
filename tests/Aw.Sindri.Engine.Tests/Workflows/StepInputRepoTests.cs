using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A Step input fills with the repo of the instance's item, also for a child with no fact:item (the gather path): the 'FillInput' activity resolves it, the workflow's history is untouched.</summary>
[Lane(TestLane.Unit)]
public sealed class StepInputRepoTests
{
    private const string Item = "ITM-0123456789abcdef0123456789abcdef";
    private static readonly BlockId _input = BlockId.From("input.context.lenses");
    private static readonly BlockId _step = BlockId.From("step.scope.gather");

    private sealed class CapturingRunner : IStepInputRunner
    {
        public StepInputContext? Seen { get; private set; }

        public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            Seen = context;
            return ValueTask.FromResult<Result<FilledOutput>>(new FilledOutput(new FilledInput(_input, index, []), new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal))));
        }
    }

    private static FillInputRequest Request(string address, string? repo) =>
        new(address, "gather", "1.1", 1, _input, _step, new Dictionary<string, WithValue>(StringComparer.Ordinal), new Dictionary<FactKey, FactValue>(), repo);

    private static StepInputActivities Activities(CapturingRunner runner, ItemReposStub repos)
    {
        var texts = new MemoryInjectionTexts();
        return new StepInputActivities(runner, texts, new StepContexts(texts, runner, repos));
    }

    [Fact]
    public async Task A_child_with_no_fact_item_fills_its_input_with_the_repo_of_the_parent_item()
    {
        var runner = new CapturingRunner();

        _ = await Activities(runner, new ItemReposStub().With(Item, "web")).Fill(Request(Item + "/scoping/1/scoping.2", repo: null), TestContext.Current.CancellationToken);

        Assert.Equal("web", runner.Seen!.Repo!.Value.Value);
    }

    [Fact]
    public async Task A_repo_the_workflow_holds_is_kept_and_the_ledger_is_not_asked()
    {
        var runner = new CapturingRunner();

        _ = await Activities(runner, new ItemReposStub(fails: true)).Fill(Request("T-1/scoping/1", repo: "api"), TestContext.Current.CancellationToken);

        Assert.Equal("api", runner.Seen!.Repo!.Value.Value);
    }

    [Fact]
    public async Task An_unreadable_ledger_fails_the_input_and_it_does_not_run()
    {
        var runner = new CapturingRunner();

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => Activities(runner, new ItemReposStub(fails: true)).Fill(Request(Item + "/scoping/1/scoping.2", repo: null), TestContext.Current.CancellationToken));

        Assert.Contains("repo-unreadable", thrown.ToString(), StringComparison.Ordinal);
        Assert.Null(runner.Seen);
    }
}
