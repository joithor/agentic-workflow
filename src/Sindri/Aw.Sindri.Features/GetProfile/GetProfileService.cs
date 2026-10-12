using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetProfile;

internal sealed class GetProfileService : IService<GetProfileRequest, GetProfileResponse>
{
    public ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetProfile: make GetProfileTests pass");
}
