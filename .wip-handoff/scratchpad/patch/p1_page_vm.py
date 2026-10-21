import re
root='/home/thor/repos/aw-wt/sindri-ui-needs-you-2/src/Sindri/Aw.Sindri.Ui/'
p=root+'ViewModels/NeedsYou/NeedsYouPageViewModel.cs'
s=open(p).read()
s=s.replace("""    internal NeedsYouPageViewModel(NeedsYouViewModel list, TimeProvider clock, IClipboard clipboard)
    {
        _list = list;
        _clock = clock;
        _clipboard = clipboard;
        _list.PropertyChanged += OnListChanged;
        Rebuild();
        _lastRow = Selected;
    }
""","""    internal NeedsYouPageViewModel(NeedsYouViewModel list, TimeProvider clock, IClipboard clipboard, DeskReporter desk)
    {
        _list = list;
        _clock = clock;
        _clipboard = clipboard;
        Bar = new NeedsYouBarViewModel(desk, clipboard, clock);
        _list.PropertyChanged += OnListChanged;
        Rebuild();
        _lastRow = Selected;
        Bar.Show(Card);
        Bar.PropertyChanged += OnBarChanged;
    }

    /// <summary>The action bar under the card (slice 2): Approve, Send back, the terminal button and the keys behind them.</summary>
    public NeedsYouBarViewModel Bar { get; }

    /// <summary>True when the card shows the request: it is not replaced by the done card.</summary>
    public bool ShowsRequest => HasSelected && !Bar.IsApproved;

    /// <summary>True when the card is replaced by the done card (the request was approved, sent back or dropped).</summary>
    public bool IsDone => HasSelected && Bar.IsApproved;

    /// <summary>Asks the page to move focus to the card, so the arrow keys read it (Enter on the list).</summary>
    internal event EventHandler? CardFocusRequested;

    /// <summary>Enter: focus goes to the card.</summary>
    internal void FocusCard() => CardFocusRequested?.Invoke(this, EventArgs.Empty);
""")
s=s.replace("""    private static readonly string[] _cardProperties =
    [
        nameof(Selected),""","""    private static readonly string[] _cardProperties =
    [
        nameof(ShowsRequest), nameof(IsDone), nameof(Selected),""")
# sync the bar whenever the card properties are reported
s=s.replace("""    private void Report(string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
""","""    private void Report(string[] names)
    {
        if (ReferenceEquals(names, _cardProperties))
        {
            Bar.Show(Card);   // before the card's words are read: another request starts a fresh bar
            Bar.Enabled = !IsCleared;
        }

        foreach (var name in names)
        {
            OnChanged(name);
        }
    }

    // The bar answered or reset: the card or the done card shows.
    private void OnBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouBarViewModel.IsApproved), StringComparison.Ordinal))
        {
            OnChanged(nameof(ShowsRequest));
            OnChanged(nameof(IsDone));
        }
    }
""")
open(p,'w').write(s)

p=root+'ViewModels/Shell/ShellViewModel.Inbox.cs'
s=open(p).read()
s=s.replace("var inbox = new NeedsYouPageViewModel(NeedsYou, _clock, clipboard);","var inbox = new NeedsYouPageViewModel(NeedsYou, _clock, clipboard, new DeskReporter(api, () => Health.ApiUrl()));")
s=s.replace("private NeedsYouPageViewModel InitInbox(IClipboard clipboard)","private NeedsYouPageViewModel InitInbox(ISindriApi api, IClipboard clipboard)")
s=s.replace("""        inbox.ItemRequested += (_, e) =>""","""        inbox.Bar.DecisionRequested += (_, e) => AnswerDecision(e.RowId, e.OptionId, e.Note);
        inbox.ItemRequested += (_, e) =>""")
s=s.replace("""    // J and K walk the inbox in its own order, never Home's capped list behind it; no other Home key acts while it shows.
    private bool InboxKeyPressed(string key)
    {
        if (!IsNeedsYouInbox || key is not ("J" or "K"))
        {
            return false;
        }

        _ = Inbox.Move(key == "J" ? 1 : -1);
        return true;
    }""","""    // J and K walk the inbox in its own order, never Home's capped list behind it; A, D and Enter act on the selected request through the action bar
    // (a key never sends a protected action: the bar copies its command); no other Home key acts while it shows.
    private bool InboxKeyPressed(string key)
    {
        if (!IsNeedsYouInbox)
        {
            return false;
        }

        switch (key)
        {
            case "J" or "K":
                _ = Inbox.Move(key == "J" ? 1 : -1);
                return true;
            case "A":
                _ = Inbox.Bar.PressApprove();
                return true;
            case "D":
                _ = Inbox.Bar.PressSendBack();
                return true;
            case "X" when Inbox.Selected is { Entry.DecisionId: not null }:
                _ = Inbox.Bar.PressSendBack();   // X stays as an alias of D on a decision row
                return true;
            case "Enter":
                Inbox.FocusCard();
                return true;
            default:
                return false;
        }
    }""")
open(p,'w').write(s)

p=root+'ViewModels/Shell/ShellViewModel.cs'
s=open(p).read()
s=s.replace("Inbox = InitInbox(clipboard);","Inbox = InitInbox(api, clipboard);")
open(p,'w').write(s)

p=root+'ViewModels/Shell/ShellViewModel.Home.cs'
s=open(p).read()
old="""        else if (request.Action.OptionId.Length > 0 && NeedsYou.Row(request.RowId) is { } row)
        {
            Decision.Show(row);
            _ = Decision.Choose(request.Action.OptionId);
        }
    }
"""
new="""        else if (request.Action.OptionId.Length > 0)
        {
            AnswerDecision(request.RowId, request.Action.OptionId, null);
        }
    }

    // A decision answered from a key, a button or the inbox's action bar: the sheet takes the option (and the note, when the bar has one) and sends them once.
    private void AnswerDecision(string rowId, string optionId, string? note)
    {
        if (NeedsYou.Row(rowId) is { } row)
        {
            Decision.Show(row);
            if (note is not null)
            {
                Decision.Note = note;
            }

            _ = Decision.Choose(optionId);
        }
    }
"""
assert old in s
s=s.replace(old,new)
open(p,'w').write(s)
