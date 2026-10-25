using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OnyxEnsure;

/// <summary>IManagedOnyx over the latest approved profile: `context.sources.onyx.managed` as the context engine checks it.</summary>
internal sealed class ManagedOnyx(IApprovedProfile approved, IContextEngine engine) : IManagedOnyx
{
    public async ValueTask<Result<bool>> Is(CancellationToken cancellationToken)
    {
        var found = await Named(cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value is { Managed: true };
    }

    public async ValueTask<Result<bool>> IsConfigured(CancellationToken cancellationToken)
    {
        var found = await Named(cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value is not null;
    }

    private async ValueTask<Result<Context.Core.Onyx.OnyxSettings?>> Named(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value is not { } profile)
        {
            return Results.Success<Context.Core.Onyx.OnyxSettings?>(value: null);
        }

        var settings = engine.CheckSettings(profile.Profile.Context, [.. (profile.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
        return Results.Success(settings.IsSuccess ? settings.Value.Onyx : null);
    }
}
