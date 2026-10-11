using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.CheckProvenance;

/// <summary>
/// Checks the database against the settings' seed policy (unconfigured, it matches only fictional example.com
/// addresses, so an install fails closed). Never an error: a failed query is 'unknown' with the reason.
/// </summary>
internal sealed class CheckProvenanceService(IProvenanceQuery query, IEvidenceSettings settings) : IService<CheckProvenanceRequest, CheckProvenanceResponse>
{
    public async ValueTask<Result<CheckProvenanceResponse>> Handle(CheckProvenanceRequest request, CancellationToken cancellationToken)
    {
        var finding = await ProvenanceCheck.RunAsync(query, settings.SeedPolicy, cancellationToken).ConfigureAwait(false);
        return new CheckProvenanceResponse(finding.Provenance, finding.Reason);
    }
}
