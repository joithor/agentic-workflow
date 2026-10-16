using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Decide;

internal sealed class DecideService : IService<DecideRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(DecideRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Decide: make DecideTests pass");
}
