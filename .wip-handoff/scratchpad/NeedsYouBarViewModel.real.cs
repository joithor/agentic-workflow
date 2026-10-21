using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar under the inbox's request card (F3 needs-you spec §3, §5). It follows the card's row and decides, per kind of
/// request, what the primary action, send back and the terminal button are, and sends nothing a catalog row calls protected: a desk
/// request is reported once through <see cref="DeskReporter"/>, a decision goes to the shell as its option, and a protected action is
/// only its command copied. A, D and Ctrl+Enter reach the same methods the buttons do.
/// </summary>
internal sealed class NeedsYouBarViewModel(DeskReporter desk, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    private const string Approve = "approve";
    private const string Revise = "revise";
    private const string Rework = "rework";
    private const string Drop = "drop";
    private static readonly TimeSpan _copiedShown = TimeSpan.FromMilliseconds(1500);

    private static readonly string[] _all =
    [
        nameof(Mode), nameof(IsPending), nameof(IsSendingBack), nameof(IsConfirmingDrop), nameof(IsApproved), nameof(ShowsActions), nameof(ApproveWord), nameof(ApproveName), nameof(IsAsking), nameof(ShowsPrimary), nameof(PrimaryEnabled),
        nameof(ShowsTerminal), nameof(TerminalWord), nameof(TerminalButton), nameof(TerminalName), nameof(ShowsSendBack), nameof(SendBackEnabled), nameof(SendBackWord), nameof(SendBackName), nameof(SendBackTooltip), nameof(ShowsEditChip),
        nameof(ShowsDrop), nameof(ShowsRowControls), nameof(Hint), nameof(HasHint), nameof(HintIsAttention), nameof(HintIsSuccess), nameof(Refusal), nameof(HasRefusal), nameof(ReasonLabel), nameof(ReasonRequired),
        nameof(ReasonPlaceholder), nameof(CanSend), nameof(SendWord), nameof(IsSending), nameof(DoneTitle), nameof(DoneBody), nameof(HasBar), nameof(MoreName), nameof(BarName),
    ];

    private NeedsYouRowViewModel? _row;
    private DeskTarget? _target;
    private bool _read;
    private bool _enabled = true;
    private bool _copied;
    private bool _sending;
    private bool _targetKnown;
    private string _done = "";   // the outcome that closed the request: approve, revise, rework or drop

    /// <summary>Raised when the bar asks to answer a decision: the shell sends it as the decision sheet would.</summary>
    internal event EventHandler<NeedsYouDecisionEventArgs>? DecisionRequested;

    /// <summary>Raised when the reason box opened: focus goes into it.</summary>
    internal event EventHandler? ReasonRequested;

    /// <summary>What the bar shows.</summary>
    public NeedsYouBarMode Mode { get; private set; }

    /// <summary>Completes when the desk run of the shown row has been read.</summary>
    internal Task Loaded { get; private set; } = Task.CompletedTask;

    /// <summary>Completes when the copied line has been shown for its time and dropped.</summary>
    internal Task CopiedExpiry { get; private set; } = Task.CompletedTask;

    /// <summary>True once the card has been scrolled to its end (a card shorter than its viewport counts as read at once). Reading to the end ends the question "Approve?".</summary>
    internal bool ReadToEnd
    {
        get => _read;
        set
        {
            if (_read == value)
            {
                return;
            }

            _read = value;
            if (value && Mode == NeedsYouBarMode.Confirming)
            {
                Mode = NeedsYouBarMode.Pending;
            }

            Refresh();
        }
    }

    /// <summary>False while the request has left the list under the card: the buttons go quiet.</summary>
    internal bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                Refresh();
            }
        }
    }

    /// <summary>The reason typed for a send back or a decline.</summary>
    public string ReasonText
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                OnChanged(nameof(CanSend));
            }
        }
    } = "";

    /// <summary>True while the bar asks something Esc withdraws: the reason box, the drop question or "Approve?".</summary>
    internal bool HasOpenQuestion => Mode is NeedsYouBarMode.SendingBack or NeedsYouBarMode.ConfirmingDrop or NeedsYouBarMode.Confirming;

    /// <summary>The More menu's words.</summary>
    public string MoreWord { get; } = Labels.NeedsYouMore;

    /// <summary>The More menu's accessible name.</summary>
    public string MoreName => Labels.NeedsYouMoreName(Subject);

    /// <summary>The menu item that drops the request.</summary>
    public string DropWord { get; } = Labels.NeedsYouDrop;

    /// <summary>The question before a drop is sent.</summary>
    public string DropAsk { get; } = Labels.NeedsYouDropAsk;

    /// <summary>The button that sends the drop.</summary>
    public string DropConfirmWord { get; } = Labels.NeedsYouDropConfirm;

    /// <summary>The words of the feature that is not built yet.</summary>
    public string EditWord { get; } = Labels.NeedsYouEditLater;

    /// <summary>The chip on it.</summary>
    public string NotAvailableWord { get; } = Labels.NeedsYouNotAvailableYet;

    /// <summary>The reason box's accessible name.</summary>
    public string ReasonName { get; } = Labels.NeedsYouReasonName;

    /// <summary>Closes the reason box or the drop question.</summary>
    public string CancelWord { get; } = Labels.NeedsYouCancel;

    /// <summary>What hovering the terminal button says.</summary>
    public string TerminalTip { get; } = Labels.NeedsYouTerminalTip;

    /// <summary>The bar's accessible name.</summary>
    public string BarName => Labels.NeedsYouBarGroupName(Subject);

    /// <summary>True when the request has a bar of its own.</summary>
    public bool HasBar => Kind != BarKind.None && (ShowsPrimary || ShowsTerminal || ShowsSendBack || IsApproved);

    /// <summary>True in the resting modes (Approve and Send back show).</summary>
    public bool IsPending => Mode is NeedsYouBarMode.Pending or NeedsYouBarMode.Confirming;

    /// <summary>True while the reason box shows.</summary>
    public bool IsSendingBack => Mode == NeedsYouBarMode.SendingBack;

    /// <summary>True while the drop question shows.</summary>
    public bool IsConfirmingDrop => Mode == NeedsYouBarMode.ConfirmingDrop;

    /// <summary>True once the request was answered.</summary>
    public bool IsApproved => Mode == NeedsYouBarMode.Approved;

    /// <summary>True when the buttons show (the resting modes).</summary>
    public bool ShowsActions => IsPending;

    /// <summary>True while the primary asks first.</summary>
    public bool IsAsking => Mode == NeedsYouBarMode.Confirming;

    /// <summary>The primary button's words: the decision's own, else "Approve", or "Approve?" while it asks.</summary>
    public string ApproveWord => Kind == BarKind.Decision ? AcceptControl?.Action.Label ?? Labels.NeedsYouApprove : IsAsking ? Labels.NeedsYouApproveAsk : Labels.NeedsYouApprove;

    /// <summary>The primary button's accessible name: the words and the request.</summary>
    public string ApproveName => Labels.NeedsYouBarName(ApproveWord, Subject);

    /// <summary>True when the primary button shows: a desk request whose step the person completes here, or a decision with a recommended option.</summary>
    public bool ShowsPrimary => Kind switch
    {
        BarKind.Desk => DeskControl is not null && !TerminalOnly,
        BarKind.Decision => AcceptControl is not null,
        _ => false,
    };

    /// <summary>True when the primary can be pressed now.</summary>
    public bool PrimaryEnabled => ShowsPrimary && _enabled && !_sending && (Kind != BarKind.Desk || _target is not null) && (Kind != BarKind.Decision || AcceptControl is { IsEnabled: true });

    /// <summary>True when the terminal button shows: a merge, or a protected action; it copies the served command and sends nothing.</summary>
    public bool ShowsTerminal => TerminalOnly || (Kind is BarKind.Settings or BarKind.Run && PrimaryControl is { IsProtected: true });

    /// <summary>The terminal button's action words ("Allow more tries").</summary>
    public string TerminalWord => Kind == BarKind.Desk ? Labels.NeedsYouApproveMerge : PrimaryControl is { } primary ? WordsOf(primary.Action) : "";

    /// <summary>The terminal button's visible words.</summary>
    public string TerminalButton => Labels.NeedsYouTerminalButton(TerminalWord);

    /// <summary>The terminal button's accessible name.</summary>
    public string TerminalName => Labels.NeedsYouTerminalName(TerminalWord, Subject);

    /// <summary>True when the send back button shows (disabled with its reason on a settings request).</summary>
    public bool ShowsSendBack => Kind switch
    {
        BarKind.Desk => DeskControl is not null || TerminalOnly,
        BarKind.Decision => RejectControl is not null,
        BarKind.Settings => true,
        _ => false,
    };

    /// <summary>True when send back can be pressed.</summary>
    public bool SendBackEnabled => Kind switch
    {
        BarKind.Desk => _enabled && _target is not null && !_sending,
        BarKind.Decision => _enabled && RejectControl is { IsEnabled: true },
        _ => false,
    };

    /// <summary>The send back button's words: the decision's own, else "Send back".</summary>
    public string SendBackWord => Kind == BarKind.Decision ? RejectControl?.Action.Label ?? Labels.NeedsYouDecline : Labels.NeedsYouSendBack;

    /// <summary>The send back button's accessible name.</summary>
    public string SendBackName => Labels.NeedsYouBarName(SendBackWord, Subject);

    /// <summary>Why send back is off; empty when it is on.</summary>
    public string SendBackTooltip => Kind == BarKind.Settings ? Labels.NeedsYouSendBackLocked : "";

    /// <summary>True for the dashed chip of the feature that is not built yet (a desk request).</summary>
    public bool ShowsEditChip => Kind == BarKind.Desk;

    /// <summary>True when the card's More menu offers dropping the request (a desk request whose run is open).</summary>
    public bool ShowsDrop => Kind == BarKind.Desk && _target is not null && !TerminalOnly;

    /// <summary>True when the card keeps the row's own controls: no bar takes the request's primary.</summary>
    public bool ShowsRowControls => Kind is BarKind.None || (Kind is BarKind.Settings or BarKind.Run && !ShowsTerminal);

    /// <summary>The line under the buttons.</summary>
    public string Hint => Mode switch
    {
        NeedsYouBarMode.Approved => _done switch
        {
            Revise or Rework => Labels.NeedsYouSentBackHint,
            Drop => Labels.NeedsYouDroppedHint,
            _ => Labels.NeedsYouApprovedHint,
        },
        NeedsYouBarMode.Confirming => Labels.NeedsYouConfirmHint,
        _ when _copied => Labels.NeedsYouCopied,
        _ when Kind == BarKind.Desk && _targetKnown && _target is null => Labels.NeedsYouNotOpen,
        _ when TerminalOnly => Labels.NeedsYouTerminalOnly,
        _ when ShowsTerminal => Labels.NeedsYouProtectedHint,
        _ when Kind == BarKind.Settings => Labels.NeedsYouKeepSettings,
        _ => "",
    };

    /// <summary>True when there is a line to show.</summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>True for the line in the attention tone (the question before approving).</summary>
    public bool HintIsAttention => Mode == NeedsYouBarMode.Confirming;

    /// <summary>True for the line in the success tone (the command was copied).</summary>
    public bool HintIsSuccess => _copied;

    /// <summary>The served refusal of the last report, as served; empty for none.</summary>
    public string Refusal { get; private set; } = "";

    /// <summary>True when a refusal shows.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>The reason box's caption.</summary>
    public string ReasonLabel { get; } = Labels.NeedsYouReasonLabel;

    /// <summary>What the box says about the text.</summary>
    public string ReasonRequired => Kind == BarKind.Decision ? Labels.NeedsYouReasonRecorded : Labels.NeedsYouReasonSentBack;

    /// <summary>The box's placeholder.</summary>
    public string ReasonPlaceholder => Kind == BarKind.Decision ? Labels.NeedsYouReasonPlaceholderDecline : Labels.NeedsYouReasonPlaceholder;

    /// <summary>True when the reason can be sent: it has text and nothing is in flight.</summary>
    public bool CanSend => IsSendingBack && _enabled && !_sending && !string.IsNullOrWhiteSpace(ReasonText);

    /// <summary>The send button's words.</summary>
    public string SendWord => SendBackWord;

    /// <summary>True while a report is in flight.</summary>
    public bool IsSending => _sending;

    /// <summary>The done card's title.</summary>
    public string DoneTitle => _row is null ? "" : _done switch
    {
        Revise or Rework => Labels.NeedsYouSentBackTitle(_row.Title),
        Drop => Labels.NeedsYouDroppedTitle(_row.Title),
        _ => Labels.NeedsYouApprovedTitle(_row.Title),
    };

    /// <summary>The done card's sentence.</summary>
    public string DoneBody => _done switch
    {
        Revise or Rework => Labels.NeedsYouSentBackBody,
        Drop => Labels.NeedsYouDroppedBody,
        _ => Labels.NeedsYouApprovedBody,
    };

    private enum BarKind
    {
        None,
        Desk,
        Decision,
        Settings,
        Run,
    }

    private string Subject => _row?.Title ?? "";

    private BarKind Kind => _row is null ? BarKind.None : NeedsYouGrouping.KindOf(_row.Entry) switch
    {
        NeedsYouGroupKind.Approvals => BarKind.Desk,
        NeedsYouGroupKind.Decisions => BarKind.Decision,
        NeedsYouGroupKind.Settings => BarKind.Settings,
        NeedsYouGroupKind.Runs => BarKind.Run,
        _ => BarKind.None,
    };

    private NeedsYouActionControl? PrimaryControl => _row?.Controls.FirstOrDefault(c => c.IsPrimary);

    // A desk request is reported by the engine's CompleteStep; the row must serve it ready for the bar to offer it.
    private NeedsYouActionControl? DeskControl => _row?.Controls.FirstOrDefault(c => c.Action is { CatalogId: "CompleteStep", IsReady: true } && c.IsEnabled);

    private NeedsYouActionControl? AcceptControl => _row?.Controls.FirstOrDefault(c => c is { IsPrimary: true, Action.OptionId.Length: > 0 });

    private NeedsYouActionControl? RejectControl => _row?.Controls.FirstOrDefault(c => c.Action is { RejectLike: true, OptionId.Length: > 0 });

    private bool TerminalOnly => Kind == BarKind.Desk && _target?.IsTerminalOnly == true;

    // The command the terminal button copies: the step's served complete command for a merge, else the action's own served command.
    private string TerminalCli => TerminalOnly ? _target?.Complete?.Cli ?? "" : PrimaryControl?.Action.Cli is { Length: > 0 } cli ? cli : _row?.Cli ?? "";

    private static string WordsOf(NeedsYouAction action) =>
        string.Equals(action.CatalogId, "ApplyCommand:answer-grant", StringComparison.Ordinal) ? Labels.NeedsYouMoreTries : action.Label;

    /// <summary>Shows a request; another one starts fresh (the box, the question and the refusal are dropped), the same one is updated in place.</summary>
    /// <param name="row">The request the card shows; null for none.</param>
    internal void Show(NeedsYouRowViewModel? row)
    {
        if (row is not null && _row is not null && string.Equals(row.Id, _row.Id, StringComparison.Ordinal))
        {
            _row = row;
            Refresh();
            return;
        }

        _row = row;
        (_target, _targetKnown, _read, _copied, _sending, _done) = (null, false, false, false, false, "");
        (Mode, ReasonText, Refusal) = (NeedsYouBarMode.Pending, "", "");
        Loaded = row is not null && Kind == BarKind.Desk && row.Entry.Subject is { Kind: "instance", Id: var instance } ? Read(instance, row.Id) : Task.CompletedTask;
        Refresh();
    }

    private async Task Read(string instance, string rowId)
    {
        var target = await desk.Load(instance).ConfigureAwait(true);
        if (_row is not null && string.Equals(_row.Id, rowId, StringComparison.Ordinal))
        {
            (_target, _targetKnown) = (target, true);
            Refresh();
        }
    }

    /// <summary>A, or the primary button: approve, accept, or copy the terminal command. On a desk request not read to the end the first press only asks.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal async Task PressApprove()
    {
        if (!IsPending || !_enabled)
        {
            return;
        }

        if (ShowsTerminal)
        {
            await CopyTerminal().ConfigureAwait(true);
        }
        else if (Kind == BarKind.Decision && AcceptControl is { IsEnabled: true } accept && _row is not null)
        {
            DecisionRequested?.Invoke(this, new NeedsYouDecisionEventArgs(_row.Id, accept.Action.OptionId, ""));
        }
        else if (Kind == BarKind.Desk && PrimaryEnabled)
        {
            if (!_read && Mode != NeedsYouBarMode.Confirming)
            {
                Mode = NeedsYouBarMode.Confirming;
                Refresh();
                return;
            }

            await Report(Approve, null).ConfigureAwait(true);
        }
    }

    /// <summary>D, or the send back button: opens the reason box. A settings request has no send back, so D does nothing there.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal Task PressSendBack()
    {
        if (IsPending && SendBackEnabled)
        {
            Mode = NeedsYouBarMode.SendingBack;
            Refresh();
            ReasonRequested?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ctrl+Enter, or the send button: sends the reason with the send back or the decline. Nothing is sent without text.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task Send()
    {
        if (!CanSend || _row is null)
        {
            return;
        }

        var reason = ReasonText.Trim();
        if (Kind == BarKind.Decision && RejectControl is { IsEnabled: true } reject)
        {
            DecisionRequested?.Invoke(this, new NeedsYouDecisionEventArgs(_row.Id, reject.Action.OptionId, reason));
            (Mode, ReasonText) = (NeedsYouBarMode.Pending, "");
            Refresh();
        }
        else if (Kind == BarKind.Desk)
        {
            await Report(TerminalOnly ? Rework : Revise, reason).ConfigureAwait(true);
        }
    }

    /// <summary>Esc, or Cancel: closes the reason box, the drop question or the "Approve?" question. What was typed stays.</summary>
    internal void Cancel()
    {
        if (Mode is NeedsYouBarMode.SendingBack or NeedsYouBarMode.ConfirmingDrop or NeedsYouBarMode.Confirming)
        {
            Mode = NeedsYouBarMode.Pending;
            Refresh();
        }
    }

    /// <summary>The More menu's "Drop this item": asks to confirm first.</summary>
    /// <returns>A task that completes when the question shows.</returns>
    internal Task RequestDrop()
    {
        if (IsPending && ShowsDrop && _enabled)
        {
            Mode = NeedsYouBarMode.ConfirmingDrop;
            Refresh();
        }

        return Task.CompletedTask;
    }

    /// <summary>The drop question's button: sends the drop.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task ConfirmDrop()
    {
        if (IsConfirmingDrop && _enabled)
        {
            await Report(Drop, null).ConfigureAwait(true);
        }
    }

    /// <summary>Copies the served command the terminal button stands for.</summary>
    /// <returns>A task that completes when the clipboard answered.</returns>
    internal async Task CopyTerminal()
    {
        if (!ShowsTerminal || TerminalCli.Length == 0)
        {
            return;
        }

        if (await clipboard.SetText(TerminalCli, CancellationToken.None).ConfigureAwait(true))
        {
            _copied = true;
            Refresh();
            CopiedExpiry = DropCopied();
        }
    }

    private async Task DropCopied()
    {
        await Task.Delay(_copiedShown, clock).ConfigureAwait(true);
        _copied = false;
        Refresh();
    }

    // One report per press: the flag is set before the first await. A refusal shows as served and leaves the request pressable.
    private async Task Report(string outcome, string? answers)
    {
        if (_target is not { } target || _sending || _row is null)
        {
            return;
        }

        var shown = _row.Id;
        (_sending, Refusal) = (true, "");
        Refresh();
        var answer = await desk.Complete(target, outcome, answers).ConfigureAwait(true);
        _sending = false;
        if (_row is null || !string.Equals(_row.Id, shown, StringComparison.Ordinal))
        {
            return;
        }

        if (answer is { IsError: true } refused)
        {
            Refusal = refused.Error.Message;
            Mode = NeedsYouBarMode.Pending;
        }
        else if (answer is not null)
        {
            (Mode, _done, ReasonText) = (NeedsYouBarMode.Approved, outcome, "");
        }

        Refresh();
    }

    private void Refresh()
    {
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }
}
