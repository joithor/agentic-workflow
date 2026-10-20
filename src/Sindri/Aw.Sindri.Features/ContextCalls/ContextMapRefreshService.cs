using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Stub (red).</summary>
internal sealed class ContextMapRefreshService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IService<RefreshContextMapRequest, JobStartedResponse> start, TimeProvider clock, ILogger<ContextMapRefreshService> logger) : IScheduledService
{
    internal static Aw.Sindri.Core.Ids.ServiceName ServiceId { get; } = Aw.Sindri.Core.Ids.ServiceName.From("context-map-refresh");

    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)));

    public Aw.Sindri.Core.Ids.ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
