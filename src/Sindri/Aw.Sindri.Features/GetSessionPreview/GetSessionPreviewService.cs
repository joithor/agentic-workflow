using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>The live read-only preview of a session's tmux pane (collector spec CA-9, CI-10).</summary>
internal sealed class GetSessionPreviewService : IService<GetSessionPreviewRequest, GetSessionPreviewResponse>
{
    public ValueTask<Result<GetSessionPreviewResponse>> Handle(GetSessionPreviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
