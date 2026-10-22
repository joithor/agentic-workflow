using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The "As of" choice (<see cref="ContextPageViewModel"/>, continued; F3 context spec 1 §2 row 1b, §9 item 1): "Approval on" reads the map version the item's scope was frozen at, "Live" reads the current one.</summary>
internal sealed partial class ContextPageViewModel
{
    private const string MapVersionNotRetained = "SND-CONTEXT-120";

    private string? _approvedVersion;

    private bool _pinned;

    /// <summary>The map version the pinned read asks for: the approved one while Approval on is chosen, else null (live).</summary>
    internal string? PinnedVersion => _pinned ? _approvedVersion : null;

    /// <summary>Whether Approval on is chosen (else Live); only possible once the item's scope names one map version. The segment sets it; <see cref="Choose"/> is the awaitable way.</summary>
    public bool IsPinned
    {
        get => _pinned;
        set => _ = Choose(value);
    }

    /// <summary>Whether Live is chosen.</summary>
    public bool IsLive => !IsPinned;

    /// <summary>Whether Approval on can be chosen.</summary>
    public bool PinnedEnabled { get; private set => Set(ref field, value); }

    /// <summary>The line under the choice when the approved map is gone and the live one shows; empty otherwise.</summary>
    public string PinnedNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Whether <see cref="PinnedNote"/> has words.</summary>
    public bool HasPinnedNote => PinnedNote.Length > 0;

    /// <summary>Opens the page for an item: reads its frozen scope, fills the starting points and offers Approval on when the scope names one map version.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scope is read and the live map is drawn.</returns>
    internal async Task OpenForItem(string itemId, CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var result = await Ports.Api.ItemScope(api, itemId, cancellationToken).ConfigureAwait(true);
        ApplyScope(result.IsSuccess ? result.Value.Scope : null);
        if (result.IsSuccess && result.Value.Scope is { } scope)
        {
            AnchorText = string.Join(' ', scope.Anchors.Select(a => a.Key).Distinct(StringComparer.Ordinal));
            await Submit(cancellationToken).ConfigureAwait(true);
        }
    }

    // No scope: Live only, with the plain not-served line. One version: Approval on, dated. Mixed versions: Live only, and the mix is named.
    private void ApplyScope(ScopeView? scope)
    {
        _approvedVersion = scope?.MapVersion;
        PinnedEnabled = _approvedVersion is not null;
        AsOfPinned = scope is { MapVersion: not null } ? Labels.ContextMapAsOfPinnedOn(scope.FrozenAt.ToOffset(Ports.Clock.GetLocalNow().Offset)) : Labels.ContextMapAsOfPinned;
        AsOfPinnedHint = scope switch
        {
            null => Labels.ContextMapPinnedNotServed,
            { MapVersion: null } => Labels.ContextMapPinnedMixed,
            _ => string.Empty,
        };
        PinnedNote = string.Empty;
        OnChanged(nameof(HasPinnedNote));
        _pinned &= PinnedEnabled;
        OnChanged(nameof(IsPinned));
        OnChanged(nameof(IsLive));
    }

    /// <summary>Chooses Approval on or Live and reads the map again for the starting points in the box.</summary>
    /// <param name="pinned">True for Approval on (refused while it is off), false for Live.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    internal async Task Choose(bool pinned, CancellationToken cancellationToken = default)
    {
        if (pinned == _pinned || (pinned && !PinnedEnabled))
        {
            return;
        }

        _pinned = pinned;
        OnChanged(nameof(IsPinned));
        OnChanged(nameof(IsLive));
        if (Words(AnchorText).Count > 0)
        {
            await Submit(cancellationToken).ConfigureAwait(true);
        }
    }

    // The approved version is purged: say so and show the live map, with Approval on off.
    private async Task FallBackToLive(CancellationToken cancellationToken)
    {
        _approvedVersion = null;
        PinnedEnabled = false;
        AsOfPinnedHint = Labels.ContextMapPinnedPruned;
        PinnedNote = Labels.ContextMapPinnedPruned;
        OnChanged(nameof(HasPinnedNote));
        _pinned = false;
        OnChanged(nameof(IsPinned));
        OnChanged(nameof(IsLive));
        await Submit(cancellationToken).ConfigureAwait(true);
    }
}
