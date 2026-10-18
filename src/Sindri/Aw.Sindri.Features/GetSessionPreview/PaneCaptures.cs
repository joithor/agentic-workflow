using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>At most one read in flight per pane (collector spec CA-9): a request that finds one running waits for its answer instead of starting a second.</summary>
internal sealed class PaneCaptures
{
    internal Task<PaneRead> Share(string socket, string pane) => throw new NotSupportedException();
}
