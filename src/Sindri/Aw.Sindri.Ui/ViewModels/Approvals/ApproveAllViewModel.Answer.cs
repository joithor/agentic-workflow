using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the two ways to answer the challenge, a hold or typed text.</summary>
internal sealed partial class ApproveAllViewModel
{
    /// <summary>True while the button is held.</summary>
    public bool IsHolding => Hold.IsHolding;

    /// <summary>True while the hold shows its fill: held, and motion is not reduced.</summary>
    public bool ShowsFill => Hold.ShowsFill;

    /// <summary>How much of the hold is done, 0 to 1.</summary>
    public double HoldProgress => Hold.Progress;

    /// <summary>The hold button's words.</summary>
    public string HoldText => Hold.Text;

    /// <summary>The hold button's accessible name: it says how many workflows it approves.</summary>
    public string HoldName => Hold.Name;

    /// <summary>The text typed in place of a hold.</summary>
    public string Typed
    {
        get => TypedBox.Text;
        set => TypedBox.Text = value;
    }

    /// <summary>True when typed text can be sent.</summary>
    public bool CanSubmitTyped => CanApprove && Typed.Trim().Length > 0;

    /// <summary>Starts the hold (the button pressed, or Space or Enter held). Does nothing without a challenge; a challenge that ran out is asked for again first.</summary>
    internal void BeginHold() => _ = Hold.Begin();

    /// <summary>One step of the fill (the view's timer steps the button itself; this is the same step for a caller with no view). The hold that reaches the server's minimum answers by itself.</summary>
    /// <returns>A task that completes when the step, and any answer it sent, is done.</returns>
    internal Task Tick() => Hold.Tick() is { } held ? Held(held) : Task.CompletedTask;

    /// <summary>The hold reached the server's minimum: it answers with the time held.</summary>
    /// <param name="heldMs">How long the button was held.</param>
    /// <returns>A task that completes when the answer is done.</returns>
    internal Task Held(int heldMs) => _challenge is { } challenge ? Redeem(new ApproveAllRequest(challenge.Challenge, HeldMs: heldMs)) : Task.CompletedTask;

    /// <summary>The button let go (or focus left it) before the hold was done: nothing is sent.</summary>
    internal void CancelHold() => Hold.Cancel();

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
            if (_asking)
            {
                return Task.CompletedTask;
            }

            Notice = Labels.ApproveExpired;
            return Reask();
        }

        return Redeem(new ApproveAllRequest(challenge.Challenge, Typed: text));
    }

    /// <summary>True when no challenge could be had and a new one can be asked for (the Try again button shows).</summary>
    public bool CanRetry => Phase == ApproveAllPhase.Plan && _plan is not null && _challenge is null && !_asking && !IsSending && Rows.Count > 0
        && !string.Equals(ErrorCode, SessionRefusal, StringComparison.Ordinal);   // an agent session is refused every time: asking again helps nobody

    /// <summary>Asks for the challenge again after it could not be had.</summary>
    /// <returns>A task that completes when the challenge answered.</returns>
    internal Task Retry()
    {
        if (!CanRetry)
        {
            return Task.CompletedTask;
        }

        (Error, ErrorCode, Notice) = (string.Empty, string.Empty, string.Empty);
        return Reask();
    }

    /// <summary>The hold button's fixed instructions for assistive tech (the count never goes into it).</summary>
    public string HoldHelp => Hold.Help;

    // The hold may start when there is a challenge to answer; one that ran out is asked for again first and the hold waits for the next press.
    private bool BeginGate()
    {
        if (!CanApprove || !OffersHold)
        {
            return false;
        }

        if (Expired)
        {
            if (!_asking)   // one new challenge at a time
            {
                Notice = Labels.ApproveExpired;
                _ = Reask();
            }

            return false;
        }

        Notice = string.Empty;
        return true;
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
        Hold.Reset();
        TypedBox.Text = string.Empty;
        Result = null;
        Progress = string.Empty;
    }
}
