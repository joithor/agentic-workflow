using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the two ways to answer the challenge, a hold or typed text.</summary>
internal sealed partial class ApproveAllViewModel
{
    /// <summary>True while the button is held.</summary>
    public bool IsHolding { get; private set => Set(ref field, value); }

    /// <summary>How much of the hold is done, 0 to 1: the fill. Under reduced motion the view shows <see cref="HoldText"/> as a stepped count instead.</summary>
    public double HoldProgress { get; private set => Set(ref field, value); }

    /// <summary>The hold button's words: the whole hold while idle, the count while held.</summary>
    public string HoldText { get; private set => Set(ref field, value); } = Labels.ApproveHold(DefaultHoldMs);

    /// <summary>The hold button's accessible name: it says how many workflows it approves.</summary>
    public string HoldName => Labels.ApproveHoldName(Rows.Count, MinHoldMs);

    /// <summary>The text typed in place of a hold.</summary>
    public string Typed
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSubmitTyped));
        }
    } = string.Empty;

    /// <summary>True when typed text can be sent.</summary>
    public bool CanSubmitTyped => CanApprove && Typed.Trim().Length > 0;

    /// <summary>Starts the hold (the button pressed, or Space or Enter held). Does nothing without a challenge; a challenge that ran out is asked for again first.</summary>
    internal void BeginHold()
    {
        if (!CanApprove || !OffersHold || IsHolding)
        {
            return;
        }

        if (Expired)
        {
            Notice = Labels.ApproveExpired;
            _ = Reask();
            return;
        }

        Notice = string.Empty;
        _holdStarted = _clock.GetTimestamp();
        (IsHolding, HoldProgress) = (true, 0);
        HoldText = Labels.ApproveHolding(0, MinHoldMs);
        Live.Say(Labels.ApproveHoldStarted);
    }

    /// <summary>One step of the fill (the view calls it on a timer while held). The hold that reaches the server's minimum answers by itself.</summary>
    /// <returns>A task that completes when the step, and any answer it sent, is done.</returns>
    internal Task Tick()
    {
        if (!IsHolding || _challenge is not { } challenge)
        {
            return Task.CompletedTask;
        }

        var held = (int)_clock.GetElapsedTime(_holdStarted).TotalMilliseconds;
        HoldProgress = Math.Min(1d, (double)held / MinHoldMs);
        HoldText = Labels.ApproveHolding(held, MinHoldMs);
        return held >= MinHoldMs ? Redeem(new ApproveAllRequest(challenge.Challenge, HeldMs: held)) : Task.CompletedTask;
    }

    /// <summary>The button let go (or focus left it) before the hold was done: nothing is sent.</summary>
    internal void CancelHold()
    {
        if (!IsHolding)
        {
            return;
        }

        (IsHolding, HoldProgress) = (false, 0);
        HoldText = Labels.ApproveHold(MinHoldMs);
        Notice = Labels.ApproveHoldAgain;
    }

    /// <summary>Sends the typed text. It must be exactly the challenge's text; anything else is said here and nothing is sent, so the challenge is not used up.</summary>
    /// <returns>A task that completes when the answer, if one was sent, is done.</returns>
    internal Task SubmitTyped()
    {
        if (!CanSubmitTyped || _challenge is not { } challenge)
        {
            return Task.CompletedTask;
        }

        var text = Typed.Trim();
        if (!string.Equals(text, TypedAlternative, StringComparison.Ordinal))
        {
            Notice = Labels.ApproveTypedMismatch(TypedAlternative);
            return Task.CompletedTask;
        }

        if (Expired)
        {
            Notice = Labels.ApproveExpired;
            return Reask();
        }

        return Redeem(new ApproveAllRequest(challenge.Challenge, Typed: text));
    }

    private bool Expired => _challenge is { } c && c.ExpiresAt <= _clock.GetUtcNow();

    private async Task Reask()
    {
        if (_url() is { } api)
        {
            await Ask(api, _session, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void ResetAnswerState()
    {
        (IsHolding, HoldProgress, Typed) = (false, 0, string.Empty);
        HoldText = Labels.ApproveHold(DefaultHoldMs);
        Result = null;
        Progress = string.Empty;
    }
}
