root='/home/thor/repos/aw-wt/sindri-ui-needs-you-2/src/Sindri/Aw.Sindri.Ui/'
p=root+'Views/NeedsYou/NeedsYouPage.axaml.cs'
s=open(p).read()
s=s.replace('''using Avalonia.Interactivity;
using Avalonia.Threading;''','''using Avalonia.Interactivity;
using Avalonia.Threading;''')
old='''    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _page = DataContext as NeedsYouPageViewModel;
    }
'''
new='''    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_page is not null)
        {
            _page.PropertyChanged -= OnPageChanged;
            _page.Bar.ReasonRequested -= OnReasonRequested;
            _page.CardFocusRequested -= OnCardFocusRequested;
        }

        _page = DataContext as NeedsYouPageViewModel;
        if (_page is not null)
        {
            _page.PropertyChanged += OnPageChanged;
            _page.Bar.ReasonRequested += OnReasonRequested;
            _page.CardFocusRequested += OnCardFocusRequested;
            NeedsYouDropItem.Header = _page.Bar.DropWord;   // a flyout's items do not take the page's data context
        }
    }

    // Another request on the card starts it at the top, and what "read to the end" means is read again once it is drawn.
    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouPageViewModel.Card), StringComparison.Ordinal))
        {
            NeedsYouCardScroll.ScrollToHome();
            Dispatcher.UIThread.Post(UpdateRead, DispatcherPriority.Loaded);
        }
    }

    private void OnCardScrolled(object? sender, ScrollChangedEventArgs e) => UpdateRead();

    // §5 open item 4: "read" is the scroll flag only, and a card shorter than its viewport is read at once. A card that is not drawn says nothing.
    private void UpdateRead()
    {
        var scroll = NeedsYouCardScroll;
        if (_page is not null && scroll.IsEffectivelyVisible && scroll.Viewport.Height > 0)
        {
            _page.Bar.ReadToEnd = scroll.Extent.Height <= scroll.Viewport.Height + 1 || scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 1;
        }
    }

    // The reason box opened (D): focus goes into it, so typing starts there.
    private void OnReasonRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = NeedsYouReason.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    // Enter on the list: focus goes to the card, where the arrow keys scroll it.
    private void OnCardFocusRequested(object? sender, EventArgs e) => _ = NeedsYouCardScroll.Focus(NavigationMethod.Tab);

    // Ctrl+Enter sends the reason and Esc closes the box; any other key is the box's own. The shell's single keys never see a key typed in a field.
    private void OnReasonKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _page?.Bar.Cancel();
            e.Handled = true;
        }
        else if (e is { Key: Key.Enter, KeyModifiers: KeyModifiers.Control or KeyModifiers.Meta })
        {
            _ = _page?.Bar.Send();
            e.Handled = true;
        }
    }

    private void OnApprove(object? sender, RoutedEventArgs e) => _ = _page?.Bar.PressApprove();

    private void OnTerminal(object? sender, RoutedEventArgs e) => _ = _page?.Bar.CopyTerminal();

    private void OnSendBack(object? sender, RoutedEventArgs e) => _ = _page?.Bar.PressSendBack();

    private void OnSend(object? sender, RoutedEventArgs e) => _ = _page?.Bar.Send();

    private void OnCancel(object? sender, RoutedEventArgs e) => _page?.Bar.Cancel();

    private void OnDropItem(object? sender, RoutedEventArgs e) => _ = _page?.Bar.RequestDrop();

    private void OnDropConfirm(object? sender, RoutedEventArgs e) => _ = _page?.Bar.ConfirmDrop();
'''
assert old in s
s=s.replace(old,new)
open(p,'w').write(s)

# page words for the key hints
p=root+'ViewModels/NeedsYou/NeedsYouPageViewModel.cs'
s=open(p).read()
s=s.replace('''    /// <summary>The link from the card's head to the item.</summary>''','''    /// <summary>The key hint of Enter.</summary>
    public string KeyOpenWord { get; } = Labels.NeedsYouKeyOpenWord;

    /// <summary>The key hint of A.</summary>
    public string KeyApproveWord { get; } = Labels.NeedsYouKeyApproveWord;

    /// <summary>The key hint of D.</summary>
    public string KeySendBackWord { get; } = Labels.NeedsYouKeySendBackWord;

    /// <summary>The link from the card's head to the item.</summary>''',1)
open(p,'w').write(s)

# Esc: the bar's question or box closes before the inbox does
p=root+'Views/Shell/ShellWindow.NeedsYou.cs'
s=open(p).read()
old='''        return shell.LeaveNeedsYouInbox();
    }'''
new='''        if (shell.IsNeedsYouInbox && shell.Inbox.Bar.HasOpenQuestion)
        {
            shell.Inbox.Bar.Cancel();   // the reason box, the drop question or "Approve?" closes first
            return true;
        }

        return shell.LeaveNeedsYouInbox();
    }'''
assert old in s
s=s.replace(old,new,1)
open(p,'w').write(s)
