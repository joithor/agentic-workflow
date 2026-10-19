using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Features.GetCode;
using Aw.Context.Features.GetHistory;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The code and history reads of the facade (S-CL, S-H): each dispatches to its slice.</summary>
internal sealed class ContextCodeHistory(IService<GetCodeRequest, CodeView> code, IService<GetHistoryRequest, HistoryView> history) : IContextCode, IContextHistory
{
    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) =>
        code.Handle(new GetCodeRequest(anchors, draft, mapVersion, scope), cancellationToken);

    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) =>
        history.Handle(new GetHistoryRequest(anchors, window, read, scope), cancellationToken);
}
