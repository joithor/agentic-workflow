using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>GET /setup: the six steps in their fixed order, each from its own source, and a source that can't be read fails only its own step.</summary>
public sealed class GetSetupTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero));

    private GetSetupService Service(InMemorySetupFacts facts) => new(facts, new ProjectorProgress(_clock));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetSetup_reports_every_step_in_order()
    {
        var service = Service(InMemorySetupFacts.AllDone());

        var result = await service.Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal([SetupStepId.Repo, SetupStepId.Profile, SetupStepId.Recipes, SetupStepId.Mcp, SetupStepId.Map, SetupStepId.Onyx], result.Value.Steps.Select(static s => s.Id));
        Assert.All(result.Value.Steps, static s => Assert.Equal(SetupState.Done, s.State));
        Assert.Equal(new ProjectorProgress(_clock).Current.AsOf, result.Value.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Titles_are_the_fixed_words_and_items_are_listed_empty_until_a_step_counts_repos()
    {
        var steps = (await Service(InMemorySetupFacts.AllDone()).Handle(A.GetSetupRequest().Build(), Token)).Value.Steps;

        Assert.Equal(["Add your repo", "Approve the profile", "Approve the recipes", "Connect your agents", "Build the map", "Set up Onyx"], steps.Select(static s => s.Title), StringComparer.Ordinal);
        Assert.All(steps, static s => Assert.Empty(s.Items));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_box_has_every_step_still_to_do_and_the_answer_is_never_an_error()
    {
        var result = await Service(new InMemorySetupFacts()).Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Steps.Count);
        Assert.All(result.Value.Steps, static s => Assert.Equal(SetupState.Todo, s.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_that_cannot_be_read_fails_only_its_own_step_and_names_its_code()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.RecipesAnswer = SindriErrors.LedgerUnavailable.With("the ledger is locked");

        var steps = (await Service(facts).Handle(A.GetSetupRequest().Build(), Token)).Value.Steps;

        var failed = Assert.Single(steps, static s => s.State == SetupState.Failed);
        Assert.Equal(SetupStepId.Recipes, failed.Id);
        Assert.Equal(SindriErrors.LedgerUnavailable.Code, failed.Error?.Code);
        Assert.DoesNotContain(SindriErrors.LedgerUnavailable.Code, failed.Detail, StringComparison.Ordinal);
        Assert.All(steps.Where(static s => s.Id != SetupStepId.Recipes), static s => Assert.Equal(SetupState.Done, s.State));
    }
}
