using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Forge;

/// <summary>The pull requests of a Sindri with no forge wired: every read fails, so a Step that needs the forge fails instead of guessing.</summary>
internal sealed class NoForgePullRequests : IPullRequests
{
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<PullView>>(WorkflowErrors.EffectFailed.With("no forge is wired to this Sindri"));
}
