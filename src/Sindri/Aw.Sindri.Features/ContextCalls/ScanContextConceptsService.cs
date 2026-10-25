using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a scan job (owner, q110): scans the newest map version unless the daily budget is spent.</summary>
internal sealed class ScanContextConceptsService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, TimeProvider clock) : IService<ScanContextConceptsRequest, ScanContextConceptsResult>
{
    public ValueTask<Result<ScanContextConceptsResult>> Handle(ScanContextConceptsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(string.Join(',', scopes, engine, jobs, clock));

    /// <summary>The job summary: what the scan did in one line.</summary>
    internal static string Summary(ScanContextConceptsResult result) => throw new NotSupportedException();
}
