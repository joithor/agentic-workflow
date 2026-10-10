using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SaveBrief;

internal sealed class SaveBriefService(IBriefStore briefs, TimeProvider clock) : IService<SaveBriefRequest, SaveBriefResponse>
{
    public async ValueTask<Result<SaveBriefResponse>> Handle(SaveBriefRequest request, CancellationToken cancellationToken)
    {
        var brief = new Brief
        {
            ToolUseId = request.ToolUseId,
            SessionId = request.SessionId,
            PromptId = request.PromptId,
            DispatchName = request.DispatchName,
            SubagentType = request.SubagentType,
            Goal = request.Goal,
            AcceptanceCriteria = request.AcceptanceCriteria,
            ProofCommand = request.ProofCommand,
            SavedAt = clock.GetUtcNow(),
        };
        var saved = await briefs.Save(brief, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new SaveBriefResponse(brief);
    }
}
