using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// A 'RecheckEffect' that always holds, standing in for engine P6 unit (b)'s recheck in the dispatch tests: the recheck
/// itself is (b)'s and has its own tests.
/// </summary>
internal static class PassingRecheck
{
    /// <summary>The worker options the host composes, with this recheck in place of the shipped one.</summary>
    public static TemporalWorkerOptions Options(IServiceProvider sindri)
    {
        var options = (TemporalWorkerOptions)sindri.GetRequiredService<TemporalWorkerOptions>().Clone();
        var shipped = options.Activities.Single(static a => a.Name == "RecheckEffect");
        _ = options.Activities.Remove(shipped);
        return options.AddActivity(RecheckAsync);
    }

    /// <summary>Every effect may run.</summary>
    [Activity("RecheckEffect")]
    public static Task<EffectRecheckResult> RecheckAsync(EffectRecheck recheck) => Task.FromResult(new EffectRecheckResult(recheck is null ? "unreachable" : null));
}
