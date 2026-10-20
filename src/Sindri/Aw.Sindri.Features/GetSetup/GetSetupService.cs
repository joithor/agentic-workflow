using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ports;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>
/// Where "set up Sindri once" stands: the six steps in their fixed order (repo, profile, recipes, mcp, map, onyx), each decided by <see cref="SetupSteps"/> from the fact its own
/// source gave. A pure read that starts nothing. A source that can't be read makes only its own step "failed" with a plain sentence, so the answer is never an error.
/// </summary>
/// <param name="facts">The sources.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="logger">Where a source that threw is recorded.</param>
internal sealed partial class GetSetupService(ISetupFacts facts, IProjectorStatus status, ILogger<GetSetupService> logger) : IService<GetSetupRequest, GetSetupResponse>
{
    public async ValueTask<Result<GetSetupResponse>> Handle(GetSetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repo = await Read("repo", facts.Repos, cancellationToken).ConfigureAwait(false);
        var profile = await Read("profile", facts.Profile, cancellationToken).ConfigureAwait(false);
        var recipes = await Read("recipes", facts.Recipes, cancellationToken).ConfigureAwait(false);
        var mcp = await Read("mcp", facts.Mcp, cancellationToken).ConfigureAwait(false);
        var map = await Read("map", facts.Map, cancellationToken).ConfigureAwait(false);
        var onyx = await Read("onyx", facts.Onyx, cancellationToken).ConfigureAwait(false);
        var profileApproved = profile.IsSuccess && profile.Value.Presence is ProfilePresence.Approved or ProfilePresence.Drifted;
        SetupStep[] steps =
        [
            Step(SetupStepId.Repo, repo, SetupSteps.Repo),
            repo.IsError ? SetupSteps.Unreadable(SetupStepId.Profile, repo.Error.Code) : Step(SetupStepId.Profile, profile, p => SetupSteps.Profile(p, repo.Value)),
            Step(SetupStepId.Recipes, recipes, r => SetupSteps.Recipes(r, profileApproved)),
            Step(SetupStepId.Mcp, mcp, SetupSteps.Mcp),
            Step(SetupStepId.Map, map, SetupSteps.Map),
            Step(SetupStepId.Onyx, onyx, SetupSteps.Onyx),
        ];
        return new GetSetupResponse(steps, status.Current.AsOf);
    }

    private static SetupStep Step<T>(SetupStepId id, Result<T> read, Func<T, SetupStep> decide) =>
        read.IsError ? SetupSteps.Unreadable(id, read.Error.Code) : decide(read.Value);

    // Boundary: a source that throws fails only its own step, like one that returns an error: the answer is never a 500.
    // Only the request's own cancellation passes; a source's inner timeout (a TaskCanceledException on a live token) fails its step.
    private async ValueTask<Result<T>> Read<T>(string step, Func<CancellationToken, ValueTask<Result<T>>> source, CancellationToken cancellationToken)
    {
        try
        {
            return await source(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogSourceFailed(logger, step, e.GetType().Name);
            return SindriErrors.ApiInternal.With("a setup source failed");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "setup: the {Step} source threw {ExceptionType}; that step is reported failed")]
    private static partial void LogSourceFailed(ILogger logger, string step, string exceptionType);
}
