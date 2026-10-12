using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProfile;

internal sealed class GetProfileService(IViewReader views, IProjectorStatus status) : IService<GetProfileRequest, GetProfileResponse>
{
    public ValueTask<Result<GetProfileResponse>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        _ = (views, status);
        throw new NotSupportedException("GetProfile: make GetProfileTests pass");
    }
}
