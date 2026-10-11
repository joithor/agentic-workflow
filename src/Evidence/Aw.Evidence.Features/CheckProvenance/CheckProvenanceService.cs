using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.CheckProvenance;

/// <summary>
/// Checks the database against the seed policy (UI_EVIDENCE_SEED_DOMAINS, UI_EVIDENCE_SEED_EMAIL_PATTERN; the defaults
/// match only fictional example.com addresses, so an unconfigured install fails closed). Never an error: a failed
/// query is 'unknown' with the reason.
/// </summary>
internal sealed class CheckProvenanceService(IProvenanceQuery query, ISecretsSource settings) : IService<CheckProvenanceRequest, CheckProvenanceResponse>
{
    internal const string DomainsVariable = "UI_EVIDENCE_SEED_DOMAINS";
    internal const string PatternVariable = "UI_EVIDENCE_SEED_EMAIL_PATTERN";

    public async ValueTask<Result<CheckProvenanceResponse>> Handle(CheckProvenanceRequest request, CancellationToken cancellationToken)
    {
        var policy = SeedPolicy.From(settings.Get(DomainsVariable), settings.Get(PatternVariable));
        var finding = await ProvenanceCheck.RunAsync(query, policy, cancellationToken).ConfigureAwait(false);
        return new CheckProvenanceResponse(finding.Provenance, finding.Reason);
    }
}
