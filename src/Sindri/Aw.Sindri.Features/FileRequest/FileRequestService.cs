using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.FileRequest;

internal sealed class FileRequestService : IService<FileRequestRequest, RequestFiledResponse>
{
    public ValueTask<Result<RequestFiledResponse>> Handle(FileRequestRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("FileRequest: make FileRequestTests pass");
}
