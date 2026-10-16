using Aw.Kernel;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Finds a request's definition and runs its checks inside the gate: the definition's precheck, then the request type's admission when it has one.</summary>
/// <param name="definitions">Every registered job definition.</param>
/// <param name="admission">The request type's admission, or null.</param>
/// <typeparam name="TRequest">The job request.</typeparam>
internal sealed class JobAdmitter<TRequest>(IEnumerable<JobDefinition> definitions, IJobAdmission<TRequest>? admission = null)
    where TRequest : notnull
{
    public void Rollback(TRequest request) => admission?.Rollback(request);

    public async ValueTask<Result<AdmittedJob>> Admit(TRequest request, CancellationToken cancellationToken)
    {
        var definition = definitions.Single(static d => d.Request == typeof(TRequest));
        var accepted = await definition.Precheck(request, cancellationToken).ConfigureAwait(false);
        if (accepted.IsError)
        {
            return accepted.Error;
        }

        if (admission is null)
        {
            return new AdmittedJob(definition, Service: null);
        }

        var service = await admission.Admit(request, cancellationToken).ConfigureAwait(false);
        return service.IsError ? service.Error : new AdmittedJob(definition, service.Value);
    }
}
