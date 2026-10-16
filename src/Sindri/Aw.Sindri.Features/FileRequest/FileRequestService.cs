using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.FileRequest;

#pragma warning disable CS9113 // Parameter is unread: the stub that the red tests compile against
internal sealed class FileRequestService(RequestTarget target, ILedgerStore ledger, HeldLease held, EventFactory events, IIdGenerator ids, ITextScrubber scrubber, ICallerContextAccessor callers)
    : IService<FileRequestRequest, RequestFiledResponse>
{
    public ValueTask<Result<RequestFiledResponse>> Handle(FileRequestRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("FileRequest: make FileRequestTests pass");
}
