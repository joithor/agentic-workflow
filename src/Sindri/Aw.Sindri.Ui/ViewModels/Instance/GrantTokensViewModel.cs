using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The Grant tokens control of a budget-parked instance (per-round budgets brief, 2026-10-21; engine spec W7, §4.2): an amount field that starts at
/// the served suggestion, and the shared hold or typed confirmation over a server challenge bound to the amount and the instance. Only the
/// person's hold or exact text sends the answer ('POST …/commands' with 'grant-tokens' and the challenge); an agent session is refused by the
/// server (SND-AUTH-002). While the catalog's row is not ready (the server has not opened the app to this answer) the control is the terminal
/// copy of the served command, as for every protected answer.
/// </summary>
internal sealed class GrantTokensViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private readonly Func<Uri?> _url;
    private readonly string _instanceId;
    private readonly string _address;
    private AppChallengeView? _challenge;
    private int? _challengeFor;   // the amount the challenge was issued for
    private bool _asking;

    /// <summary>The control over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="catalog">The command catalog: its grant-tokens row says whether the app may confirm.</param>
    /// <param name="clock">The clock for the hold, the challenge's expiry and the terminal copy.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="instance">The instance's id (the route) and its address (the challenge's subject).</param>
    /// <param name="served">The status answer's served command line, which carries the suggested amount ("--grant-tokens 1M").</param>
    /// <param name="reducedMotion">True when motion is reduced.</param>
    internal GrantTokensViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock, Func<Uri?> url, (string InstanceId, string Address) instance, string served, Func<bool>? reducedMotion = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(url);
        (_api, _clock, _url, _instanceId, _address) = (api, clock, url, instance.InstanceId, instance.Address);
        Suggested = SuggestionOf(served);
        Hold = new HoldButtonViewModel(AppConfirm.GrantTokens, catalog, clock, reducedMotion) { Gate = BeginGate };
        TypedBox = new TypedConfirmBoxViewModel(AppConfirm.GrantTokens, catalog, clock);
        Amount = Suggested;   // after the boxes: setting an amount clears the typed one
        Terminal = Hold.IsReady ? null : new ProtectedActionViewModel(new ProtectedAction(Labels.TerminalVerb(AppConfirm.GrantTokens), served, instance.Address), catalog.Clipboard, clock);
        Hold.Cancelled += (_, _) => Notice = Labels.ApproveHoldAgain;
    }

    /// <summary>Raised once the grant was applied: the frame reads the instance again.</summary>
    internal event EventHandler? Granted;

    /// <summary>The amount the park suggested ("1M"), as the served command spells it.</summary>
    public string Suggested { get; }

    /// <summary>The hold button (the shared control: it times and draws; this control owns the challenge).</summary>
    public HoldButtonViewModel Hold { get; }

    /// <summary>The typed alternative, on the shared control.</summary>
    public TypedConfirmBoxViewModel TypedBox { get; }

    /// <summary>The terminal copy while the catalog row is not ready; null when the app confirms itself.</summary>
    public ProtectedActionViewModel? Terminal { get; }

    /// <summary>True when the catalog says the app may confirm this answer.</summary>
    public bool IsReady => Terminal is null;

    /// <summary>The label over the amount field and on the control.</summary>
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    public string Title => Labels.TerminalVerb(AppConfirm.GrantTokens);

    /// <summary>The amount field's accessible name.</summary>
    public string AmountName => "Tokens to grant for this round";
#pragma warning restore CA1822, MA0041

    /// <summary>The tokens to grant as the person writes them ("1M", "500k" or a plain number); it starts at the suggestion.</summary>
    public string Amount
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(AmountTokens));
            OnChanged(nameof(HasAmount));
            OnChanged(nameof(ShowsAmountProblem));
            _challenge = null;   // a challenge is bound to its amount
            TypedBox.Clear();
        }
    } = string.Empty;

    /// <summary>The amount as a number; null when it is not a whole number of tokens of at least 1.</summary>
    public int? AmountTokens => TokenCount.TryParse(Amount, out var tokens) ? tokens : null;

    /// <summary>True when the amount reads as tokens.</summary>
    public bool HasAmount => AmountTokens is not null;

    /// <summary>True when the field holds text that is not an amount.</summary>
    public bool ShowsAmountProblem => Amount.Trim().Length > 0 && !HasAmount;

    /// <summary>What the person is told: a held-off hold, an expired challenge.</summary>
    public string Notice { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's refusal in its own words (an amount outside the bound, a stale challenge, an agent session); empty when none.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the answer is on its way.</summary>
    public bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>True once the grant was applied.</summary>
    public bool IsDone { get; private set => Set(ref field, value); }

    /// <summary>True when typed text can be sent.</summary>
    public bool CanSubmitTyped => HasAmount && _challenge is not null && TypedBox.CanSubmit && !IsSending;

    /// <summary>Asks for the challenge of the amount now written; the view calls it when the field is left or the control opens.</summary>
    /// <returns>A task that completes when the challenge answered.</returns>
    internal async Task Prepare()
    {
        if (!IsReady || _asking || AmountTokens is not { } tokens || _url() is not { } api)
        {
            return;
        }

        _asking = true;
        try
        {
            var asked = await _api.IssueAppChallenge(api, new AppChallengeRequest(string.Create(CultureInfo.InvariantCulture, $"grant-tokens {tokens} {_address}")), CancellationToken.None).ConfigureAwait(true);
            if (asked.IsError)
            {
                Error = asked.Error.Message;
                return;
            }

            if (AmountTokens != tokens)
            {
                return;   // the field changed while the challenge was on its way: it is not this amount's
            }

            (_challenge, _challengeFor, Error) = (asked.Value, tokens, string.Empty);
            Hold.Offer(asked.Value);
            TypedBox.Offer(asked.Value);
            OnChanged(nameof(CanSubmitTyped));
        }
        finally
        {
            _asking = false;
        }
    }

    /// <summary>Starts the hold (the button pressed, or Space or Enter held). Without a usable challenge it asks for one and the next press holds.</summary>
    internal void BeginHold() => _ = Hold.Begin();

    /// <summary>The button let go before the hold was done: nothing is sent.</summary>
    internal void CancelHold() => Hold.Cancel();

    /// <summary>One step of the fill (the view's timer steps the button itself; this is the same step for a caller with no view). The hold that reaches the server's minimum answers by itself.</summary>
    /// <returns>A task that completes when the step, and any answer it sent, is done.</returns>
    internal Task Tick() => Hold.Tick() is { } held ? Held(held) : Task.CompletedTask;

    /// <summary>The hold reached the server's minimum: it answers with the time held.</summary>
    /// <param name="heldMs">How long the button was held.</param>
    /// <returns>A task that completes when the answer is done.</returns>
    internal Task Held(int heldMs) => _challenge is { } challenge ? Send(new ChallengeAnswerBody(challenge.Challenge, HeldMs: heldMs)) : Task.CompletedTask;

    /// <summary>Sends the typed text, which must be exactly the challenge's; anything else is said and nothing is sent.</summary>
    /// <returns>A task that completes when the answer, if one was sent, is done.</returns>
    internal Task SubmitTyped() =>
        _challenge is { } challenge && TypedBox.Submit() is { } text ? Send(new ChallengeAnswerBody(challenge.Challenge, Typed: text)) : Task.CompletedTask;

    // The hold may start when a challenge for this very amount is there and has not run out; otherwise one is asked for and the hold waits for the next press.
    private bool BeginGate()
    {
        if (!HasAmount || IsSending || IsDone)
        {
            return false;
        }

        if (_challenge is { } challenge && _challengeFor == AmountTokens && challenge.ExpiresAt > _clock.GetUtcNow())
        {
            Notice = string.Empty;
            return true;
        }

        _challenge = null;
        Notice = Labels.ApproveExpired;
        _ = Prepare();
        return false;
    }

    // One answer goes once: the guard is set before the first await, and the challenge is used up by anything but a short hold.
    private async Task Send(ChallengeAnswerBody answer)
    {
        if (IsSending || AmountTokens is not { } tokens || _url() is not { } api)
        {
            return;
        }

        (IsSending, Error) = (true, string.Empty);
        Hold.Busy();
        TypedBox.SetSending(sending: true);
        Result<ApplyCommandAccepted> applied;
        try
        {
            applied = await _api.ApplyCommand(api, _instanceId, ApplyCommandBody.GrantTokens(_address, tokens, answer), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            IsSending = false;
            TypedBox.SetSending(sending: false);
        }

        _challenge = null;   // one use, then gone
        if (applied.IsError)
        {
            Error = applied.Error.Message;
            Hold.Reset();
            return;
        }

        IsDone = true;
        Granted?.Invoke(this, EventArgs.Empty);
    }

    // "--grant-tokens 1M" gives "1M"; a command without the flag gives nothing to suggest.
    private static string SuggestionOf(string served)
    {
        var words = served.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var at = Array.IndexOf(words, "--grant-tokens");
        return at >= 0 && at + 1 < words.Length && TokenCount.TryParse(words[at + 1], out _) ? words[at + 1] : string.Empty;
    }
}
