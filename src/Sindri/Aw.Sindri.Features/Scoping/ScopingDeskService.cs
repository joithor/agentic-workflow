using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'scoping desk': a block Step's code, run at the desk (scoping spec §8.2).</summary>
internal sealed class ScopingDeskService : IService<ScopingDeskRequest, ScopingDeskResponse>
{
    public ValueTask<Result<ScopingDeskResponse>> Handle(ScopingDeskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var block = DeskBlocks.Resolve(request.Block);
        return ValueTask.FromResult(block is null
            ? SindriErrors.InvalidInput.With($"the desk helper has no code for '{request.Block}' yet; it covers {string.Join(", ", DeskBlocks.Covered)} (fix: complete that Step by hand with `sindri workflow complete`)")
            : DeskBlocks.Run(block, new ScopeFacts(request.Facts)));
    }
}
