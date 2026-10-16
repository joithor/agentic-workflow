using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Jobs;

/// <summary>The default validator of a job request: it accepts everything (a job's own checks run in the definition's precheck and in the work).</summary>
/// <typeparam name="TRequest">The job request.</typeparam>
internal sealed class JobAcceptAllValidator<TRequest> : IValidator<TRequest>
{
    public Result<Unit> Validate(TRequest value) => Unit.Value;
}
