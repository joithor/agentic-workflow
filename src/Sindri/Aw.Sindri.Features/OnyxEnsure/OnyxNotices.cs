using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxEnsure;

/// <summary>IOnyxNotices: <see cref="OnyxNoticeRules"/> over the approved profile, state.json, the declined marker and serve's note.</summary>
internal sealed class OnyxNotices(IManagedOnyx managed, IOnyxWorkspace workspace, OnyxFacts facts) : IOnyxNotices
{
    public async ValueTask<Result<OnyxNotice?>> Current(CancellationToken cancellationToken)
    {
        var isManaged = await managed.Is(cancellationToken).ConfigureAwait(false);
        var state = workspace.ReadState();
        var setUp = state is not null && state.Steps.ContainsKey(OnyxSteps.Start);
        var declined = workspace.Read(facts.Paths.Declined) is { IsSuccess: true, Value: not null };
        var ensure = workspace.Read(facts.Paths.Ensure) is { IsSuccess: true } read ? read.Value?.Trim() : null;
        return Results.Success(OnyxNoticeRules.Decide(isManaged is { IsSuccess: true, Value: true }, setUp, declined, ensure));
    }
}
