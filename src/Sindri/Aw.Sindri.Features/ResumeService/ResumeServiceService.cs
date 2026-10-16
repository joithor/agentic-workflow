using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ResumeService;

/// <summary>Resumes a service (API P6 unit D): SND-SVC-404 for an unknown one, then one 'service.resumed'. Red until then.</summary>
internal sealed class ResumeServiceService : IService<ResumeServiceRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(ResumeServiceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit D");
}
