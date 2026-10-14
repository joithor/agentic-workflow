using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ReadLedger;

internal sealed class ReadLedgerService : IService<ReadLedgerRequest, LedgerPage>
{
    public ValueTask<Result<LedgerPage>> Handle(ReadLedgerRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ReadLedger: make ReadLedgerTests pass");
}
