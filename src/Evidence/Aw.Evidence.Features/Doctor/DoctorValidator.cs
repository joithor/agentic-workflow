using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Doctor;

internal sealed class DoctorValidator : IValidator<DoctorRequest>
{
    public Result<Unit> Validate(DoctorRequest value) => Unit.Value;
}
