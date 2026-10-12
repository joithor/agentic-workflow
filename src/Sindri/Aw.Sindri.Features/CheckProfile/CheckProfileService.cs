using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CheckProfile;

internal sealed class CheckProfileService : IService<CheckProfileRequest, CheckProfileResponse>
{
    public ValueTask<Result<CheckProfileResponse>> Handle(CheckProfileRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("CheckProfile: P2 unit C makes the profile-watch tests pass");
}
