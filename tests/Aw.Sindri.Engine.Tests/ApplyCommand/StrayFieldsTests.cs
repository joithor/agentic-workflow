using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>Cancel, takeover and handback take only the address and the nonce: a stray field would change the nonce claim, so it is refused (review fix C3).</summary>
public sealed class StrayFieldsTests
{
    private const string Address = "T-1/step/1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<ApplyCommandKind, AnswerVariant?, string?, int?, string?> Stray => new()
    {
        { ApplyCommandKind.Takeover, AnswerVariant.Grant, null, 5, null },
        { ApplyCommandKind.Handback, AnswerVariant.Accept, null, null, null },
        { ApplyCommandKind.Cancel, null, "why", null, null },
        { ApplyCommandKind.Cancel, null, null, 3, null },
        { ApplyCommandKind.Handback, null, null, null, "go" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Stray))]
    public async Task A_stray_field_on_a_bare_command_is_refused_as_invalid_input(ApplyCommandKind kind, AnswerVariant? answer, string? text, int? amount, string? verb)
    {
        var service = Service();
        var request = new ApplyCommandRequest(Address, kind, answer, text, amount, verb is null ? null : CommandVerb.From(verb), Nonce: null, Challenge: null);

        var applied = await service.Handle(request, Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, applied.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stray_challenge_on_a_bare_command_is_refused_as_invalid_input()
    {
        var service = Service();
        var request = new ApplyCommandRequest(Address, ApplyCommandKind.Takeover, Answer: null, Text: null, Amount: null, Verb: null, Nonce: null, new ChallengeAnswer(ChallengeId.For(Guid.Empty), "x"));

        var applied = await service.Handle(request, Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, applied.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bare_takeover_passes_the_argument_check()
    {
        var service = Service();
        var request = new ApplyCommandRequest(Address, ApplyCommandKind.Takeover, Answer: null, Text: null, Amount: null, Verb: null, Nonce: null, Challenge: null);

        var applied = await service.Handle(request, Token);

        Assert.NotEqual(SindriErrors.InvalidInput.Code, applied.Error.Code, StringComparer.Ordinal);   // it goes on to resolve the address, which this harness has none of
    }

    private static ApplyCommandService Service()
    {
        var harness = new SliceHarness();
        var instances = new NoInstances();
        return new ApplyCommandService(instances, instances, new NoApprovals(), new InMemoryCommandNonces(harness.Clock), harness.Callers);
    }

    private sealed class NoApprovals : IApprovedRecipes
    {
        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
