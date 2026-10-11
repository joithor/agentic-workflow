using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ValidateProfile;

internal sealed class ValidateProfileService(IProfileSource live, IApprovals approvals) : IService<ValidateProfileRequest, ValidateProfileResponse>
{
    public async ValueTask<Result<ValidateProfileResponse>> Handle(ValidateProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return new ValidateProfileResponse(loaded.Value.Hash, loaded.Value.Source, approved.IsSuccess && approved.Value?.Hash == loaded.Value.Hash);
    }
}
