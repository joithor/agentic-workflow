using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The work of the approve-all job: it takes the plan the start redeemed and hands it to the shipped-recipe approval, reporting a line per recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllRecipesServiceTests
{
    private readonly RedeemedPlans _kept = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ApproveAllRecipesRequest Request, RedeemedPlan Plan)> Redeemed(FakeShippedRecipes shipped)
    {
        var plan = (await shipped.Plan(Token)).Value;
        return (new ApproveAllRecipesRequest(), RedeemedPlan.For(plan, CommandId.From(new Guid(192, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))));
    }

    [Fact]
    public async Task The_job_approves_the_redeemed_plan_and_reports_each_recipe_by_name()
    {
        var shipped = new FakeShippedRecipes(OneRefused());
        var (request, plan) = await Redeemed(shipped);
        _kept.Keep(request, plan);
        var lines = new Lines();
        var jobs = new FakeJobContextAccessor();
        using var inJob = jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), ApproveAllRecipesAdmission.Kind, lines));
        var service = new ApproveAllRecipesService(_kept, shipped, jobs);

        var result = (await service.Handle(request, Token)).Value;

        Assert.Equal([Step, Note], result.Approved.Select(static a => a.Id));
        Assert.Equal([Item], result.Refused.Select(static r => r.Id));
        Assert.Equal([(Review, Item)], result.Skipped.Select(static s => (s.Id, s.Because)));
        Assert.Equal(["One step", "Note", "Work item", "Review loop"], lines.Seen);
        Assert.Equal("2 approved, 1 refused, 1 skipped", ApproveAllRecipesService.Summary(result));
    }

    [Fact]
    public async Task A_job_without_a_redeemed_plan_approves_nothing()
    {
        var shipped = new FakeShippedRecipes(StepAndItem());
        var (request, _) = await Redeemed(shipped);

        var result = await new ApproveAllRecipesService(_kept, shipped, new FakeJobContextAccessor()).Handle(request, Token);

        Assert.True(result.IsError);
        Assert.Equal(0, shipped.Applied);
    }

    [Fact]
    public async Task A_plan_is_taken_once()
    {
        var shipped = new FakeShippedRecipes(StepAndItem());
        var (request, plan) = await Redeemed(shipped);
        _kept.Keep(request, plan);
        var service = new ApproveAllRecipesService(_kept, shipped, new FakeJobContextAccessor());

        _ = await service.Handle(request, Token);
        var again = await service.Handle(request, Token);

        Assert.True(again.IsError);
        Assert.Equal(1, shipped.Applied);
    }

    private sealed class Lines : IProgress<string>
    {
        public List<string> Seen { get; } = [];

        public void Report(string value) => Seen.Add(value);
    }
}
