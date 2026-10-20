using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ports;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>
/// Where "set up Sindri once" stands: the six steps in their fixed order (repo, profile, recipes, mcp, map, onyx), each decided by <see cref="SetupSteps"/> from the fact its own
/// source gave. A pure read that starts nothing. A source that can't be read makes only its own step "failed" with a plain sentence, so the answer is never an error.
/// </summary>
/// <param name="facts">The sources.</param>
/// <param name="clock">Dates the answer.</param>
internal sealed class GetSetupService(ISetupFacts facts, TimeProvider clock) : IService<GetSetupRequest, GetSetupResponse>
{
    public async ValueTask<Result<GetSetupResponse>> Handle(GetSetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repo = await facts.Repos(cancellationToken).ConfigureAwait(false);
        var profile = await facts.Profile(cancellationToken).ConfigureAwait(false);
        var recipes = await facts.Recipes(cancellationToken).ConfigureAwait(false);
        var mcp = await facts.Mcp(cancellationToken).ConfigureAwait(false);
        var map = await facts.Map(cancellationToken).ConfigureAwait(false);
        var onyx = await facts.Onyx(cancellationToken).ConfigureAwait(false);
        SetupStep[] steps =
        [
            Step(SetupStepId.Repo, repo, SetupSteps.Repo),
            Step(SetupStepId.Profile, profile, SetupSteps.Profile),
            Step(SetupStepId.Recipes, recipes, SetupSteps.Recipes),
            Step(SetupStepId.Mcp, mcp, SetupSteps.Mcp),
            Step(SetupStepId.Map, map, SetupSteps.Map),
            Step(SetupStepId.Onyx, onyx, SetupSteps.Onyx),
        ];
        return new GetSetupResponse(steps, clock.GetUtcNow());
    }

    private static SetupStep Step<T>(SetupStepId id, Result<T> read, Func<T, SetupStep> decide) =>
        read.IsError ? SetupSteps.Unreadable(id, read.Error.Code) : decide(read.Value);
}
