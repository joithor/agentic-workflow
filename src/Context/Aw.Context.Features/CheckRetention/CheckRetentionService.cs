using System.Globalization;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckRetention;

/// <summary>Doctor 'context:retention' (spec §6.2): warns while expired exports or retained versions past the last 20 are still present.</summary>
internal sealed class CheckRetentionService(IGraphStore store, IExportStore exports, TimeProvider clock) : IService<CheckRetentionRequest, MapHealth>
{
    private const string Name = "context:retention";

    public async ValueTask<Result<MapHealth>> Handle(CheckRetentionRequest request, CancellationToken cancellationToken)
    {
        var files = await exports.List(cancellationToken).ConfigureAwait(false);
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (files.IsError || versions.IsError)
        {
            var error = files.IsError ? files.Error : versions.Error;
            return new MapHealth(Name, MapHealthState.Fail, $"{error.Code}: {error.Message}", "check $AW_SINDRI_HOME/context permissions");
        }

        var expired = MapRetention.ExpiredExports(files.Value, clock.GetUtcNow()).Count;
        var excess = MapRetention.ExcessVersions(versions.Value).Count;
        if (expired + excess == 0)
        {
            return new MapHealth(Name, MapHealthState.Ok, "no expired exports; at most 20 retained map versions", Fix: null);
        }

        var detail = string.Create(CultureInfo.InvariantCulture,
            $"{expired} export{(expired == 1 ? string.Empty : "s")} older than 14 days and {excess} map version{(excess == 1 ? string.Empty : "s")} past the last 20 still present");
        return new MapHealth(Name, MapHealthState.Warn, detail, "the context-purge service removes them: check it is enabled in the profile's services");
    }
}
