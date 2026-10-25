using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-concept-scan' background service (owner, q110): starts a 'ScanContextConcepts' job for each new map version, which is how a 'context.map.built' reaches the scan. A tick reads the newest map version
/// (the latest draft, else the current one), and starts the scan when no scan job succeeded for that version, none is running and none failed in the last <see cref="FailureWait"/>. Over budget, the job itself ends
/// with that reason (the count and the limit live in <see cref="ConceptScanBudget"/>), so a busy day of builds costs at most <see cref="ConceptScanLimits.MaxScansPerDay"/> scans and never a failed build. A profile
/// with no approval, or with no map yet, is a quiet tick.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine: the newest map version.</param>
/// <param name="jobs">The 'jobs' view: the scans so far.</param>
/// <param name="starts">The ScanContextConcepts command chain.</param>
/// <param name="clock">Dates the failure wait.</param>
/// <param name="logger">Logs a refused start.</param>
internal sealed class ContextConceptScanService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IService<ScanContextConceptsRequest, JobStartedResponse> starts, TimeProvider clock, ILogger<ContextConceptScanService> logger) : IScheduledService
{
    /// <summary>How long a failed scan keeps the next one from starting.</summary>
    internal static TimeSpan FailureWait { get; } = TimeSpan.FromMinutes(10);

    internal static JobKind Kind { get; } = JobKind.From("ScanContextConcepts");

    internal static ServiceName ServiceId { get; } = ServiceName.From("context-concept-scan");

    /// <summary>Runs without a profile entry, every minute: a tick is one state read and one jobs read.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)));

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new NotSupportedException(string.Join(',', scopes, engine, jobs, starts, clock, logger));
}
