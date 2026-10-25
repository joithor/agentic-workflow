using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetLabel;

internal sealed class SetLabelService : IService<SetLabelRequest, SetLabelResponse>
{
    public ValueTask<Result<SetLabelResponse>> Handle(SetLabelRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SetLabel: make SetLabelTests pass (cutover unit 6.3)");
}
