using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// The apply-command service (<see cref="ApplyCommandService"/>) plus, for a takeover that applied, opening the Step's live session in the viewer (issue 1004). The takeover has
/// already happened when the viewer is tried, so whatever the viewer does the Step stays taken over; a repeat of a takeover that had already applied opens nothing again.
/// </summary>
internal sealed class TakeoverViewingCommand : IService<ApplyCommandRequest, ApplyCommandResponse>
{
    private readonly IService<ApplyCommandRequest, ApplyCommandResponse> _inner;
    private readonly TakeoverViewing _viewing;

    /// <summary>The product's: the apply-command service itself (resolving the service interface here would resolve the chain around this one).</summary>
    /// <param name="inner">Applies the command.</param>
    /// <param name="viewing">Opens the session.</param>
    [ActivatorUtilitiesConstructor]
    public TakeoverViewingCommand(ApplyCommandService inner, TakeoverViewing viewing)
        : this((IService<ApplyCommandRequest, ApplyCommandResponse>)inner, viewing)
    {
    }

    /// <summary>Any service that applies the command.</summary>
    /// <param name="inner">Applies the command.</param>
    /// <param name="viewing">Opens the session.</param>
    public TakeoverViewingCommand(IService<ApplyCommandRequest, ApplyCommandResponse> inner, TakeoverViewing viewing)
    {
        _inner = inner;
        _viewing = viewing;
    }

    public async ValueTask<Result<ApplyCommandResponse>> Handle(ApplyCommandRequest request, CancellationToken cancellationToken)
    {
        var applied = await _inner.Handle(request, cancellationToken).ConfigureAwait(false);
        if (applied.IsError || request.Kind != ApplyCommandKind.Takeover || applied.Value.AlreadyApplied)
        {
            return applied;
        }

        var opened = await _viewing.Open(applied.Value.Result.Instance.Address, applied.Value.Nonce, cancellationToken).ConfigureAwait(false);
        return opened is null ? applied : applied.Value with { Viewer = opened.Viewer, Attach = opened.Attach };
    }
}
