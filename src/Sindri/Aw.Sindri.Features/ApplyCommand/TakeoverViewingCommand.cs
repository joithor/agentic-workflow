using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// The apply-command service (<see cref="ApplyCommandService"/>) plus, for a takeover that applied, opening the Step's live session in the viewer (issue 1004). The takeover has
/// already happened when the viewer is tried, so whatever the viewer does the Step stays taken over; a repeat of a takeover that had already applied opens nothing again.
/// </summary>
/// <param name="inner">Applies the command.</param>
/// <param name="viewing">Opens the session.</param>
internal sealed class TakeoverViewingCommand(IService<ApplyCommandRequest, ApplyCommandResponse> inner, TakeoverViewing viewing) : IService<ApplyCommandRequest, ApplyCommandResponse>
{
    /// <summary>The product's: the apply-command service itself (resolving the service interface here would resolve the chain around this one).</summary>
    /// <param name="service">Applies the command.</param>
    /// <param name="viewing">Opens the session.</param>
    [ActivatorUtilitiesConstructor]
    public TakeoverViewingCommand(ApplyCommandService service, TakeoverViewing viewing)
        : this((IService<ApplyCommandRequest, ApplyCommandResponse>)service, viewing)
    {
    }

    public async ValueTask<Result<ApplyCommandResponse>> Handle(ApplyCommandRequest request, CancellationToken cancellationToken)
    {
        var applied = await inner.Handle(request, cancellationToken).ConfigureAwait(false);
        if (applied.IsError || request.Kind != ApplyCommandKind.Takeover || applied.Value.AlreadyApplied)
        {
            return applied;
        }

        var opened = await viewing.Open(applied.Value.Result.Instance.Address, applied.Value.Nonce, cancellationToken).ConfigureAwait(false);
        return opened is null ? applied : applied.Value with { Viewer = opened.Viewer, Attach = opened.Attach };
    }
}
