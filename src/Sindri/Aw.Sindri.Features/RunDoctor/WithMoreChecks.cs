using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Appends every registered <see cref="IDoctorChecks"/> to the core report and applies the exit rule to the whole list.</summary>
/// <param name="inner">The core doctor.</param>
/// <param name="more">The other checks, in registration order.</param>
internal sealed class WithMoreChecks(IService<RunDoctorRequest, RunDoctorResponse> inner, IEnumerable<IDoctorChecks> more)
    : Decorated<RunDoctorRequest, RunDoctorResponse>(inner)
{
    public override async ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken)
    {
        var core = await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        if (core.IsError)
        {
            return core;
        }

        var checks = new List<DoctorCheck>(core.Value.Checks);
        foreach (var source in more)
        {
            checks.AddRange(await source.Run(cancellationToken).ConfigureAwait(false));
        }

        return new RunDoctorResponse(checks, DoctorRules.ExitCode(checks));
    }
}
