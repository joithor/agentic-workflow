root='/home/thor/repos/aw-wt/sindri-ui-needs-you-2/tests/Aw.Sindri.Ui.Tests/'

# --- page view model tests
p=root+'NeedsYou/NeedsYouPageViewModelTests.cs'
s=open(p).read()
s=s.replace('''    private readonly FakeClipboard _clipboard = new();
''','''    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();
''',1)
s=s.replace("return (list, new NeedsYouPageViewModel(list, _clock, _clipboard));","return (list, new NeedsYouPageViewModel(list, _clock, _clipboard, new DeskReporter(_server, () => new Uri(\"http://127.0.0.1:7410/\"))));")
# append new tests before the final brace
idx=s.rstrip().rfind('}')
new='''
    [Fact]
    public async Task The_action_bar_follows_the_card_and_a_new_request_starts_it_fresh()
    {
        var (list, page) = await Page(NeedsYouRows.Desk(), NeedsYouRows.Decision());
        list.Select(NeedsYouRows.Desk().Id);
        await page.Bar.Loaded;
        page.Bar.ReadToEnd = true;
        await page.Bar.PressSendBack();
        Assert.Equal(NeedsYouBarMode.SendingBack, page.Bar.Mode);

        _ = page.Move(1);

        Assert.Equal((NeedsYouBarMode.Pending, false, "Accept"), (page.Bar.Mode, page.Bar.ReadToEnd, page.Bar.ApproveWord));
    }

    [Fact]
    public async Task Approving_replaces_the_card_with_the_done_card_until_the_user_moves_on()
    {
        var (list, page) = await Page(NeedsYouRows.Desk(), NeedsYouRows.Decision());
        list.Select(NeedsYouRows.Desk().Id);
        await page.Bar.Loaded;
        page.Bar.ReadToEnd = true;

        await page.Bar.PressApprove();

        Assert.Equal((true, false, "Approved: Try the engine"), (page.IsDone, page.ShowsRequest, page.Bar.DoneTitle));
        _ = page.Move(1);
        Assert.Equal((false, true), (page.IsDone, page.ShowsRequest));
    }

    [Fact]
    public async Task A_request_that_left_the_list_keeps_its_card_and_quiets_the_bar()
    {
        var (list, page) = await Page(NeedsYouRows.Desk(), NeedsYouRows.Decision());
        list.Select(NeedsYouRows.Desk().Id);
        await page.Bar.Loaded;
        page.Bar.ReadToEnd = true;

        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], ""));

        Assert.True(page.IsCleared);
        Assert.False(page.Bar.PrimaryEnabled);
    }
'''
s=s[:idx]+new.lstrip('\n')+s[idx:]
open(p,'w').write(s)

# --- shell key tests
p=root+'Shell/ShellInboxTests.cs'
s=open(p).read()
idx=s.rstrip().rfind('}')
new='''
    private void ServeDeskInstance() =>
        _server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));

    [Fact]
    public Task A_asks_first_on_an_unread_desk_request_and_a_second_A_reports_it() => UiThread.Run(async () =>
    {
        ServeDeskInstance();
        var shell = await Shell(NeedsYouRows.Desk());
        shell.OpenNeedsYouInbox();
        await shell.Inbox.Bar.Loaded;

        _ = shell.Press("A");
        Assert.Equal(NeedsYouBarMode.Confirming, shell.Inbox.Bar.Mode);
        _ = shell.Press("A");

        for (var i = 0; i < 400 && _server.CompleteCalls.Count == 0; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal("approve", _server.CompleteCalls.Single().Body.Outcome);
    }, Token);

    [Fact]
    public Task D_opens_the_reason_box_and_X_does_the_same_on_a_decision() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Decision());
        shell.OpenNeedsYouInbox();

        _ = shell.Press("D");
        Assert.Equal(NeedsYouBarMode.SendingBack, shell.Inbox.Bar.Mode);
        shell.Inbox.Bar.Cancel();
        _ = shell.Press("X");
        Assert.Equal(NeedsYouBarMode.SendingBack, shell.Inbox.Bar.Mode);
    }, Token);

    [Fact]
    public Task D_does_nothing_on_a_settings_request_and_E_is_inert() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Profile());
        shell.OpenNeedsYouInbox();

        _ = shell.Press("D");
        _ = shell.Press("E");

        Assert.Equal(NeedsYouBarMode.Pending, shell.Inbox.Bar.Mode);
    }, Token);

    [Fact]
    public Task Enter_asks_the_page_to_focus_the_card() => UiThread.Run(async () =>
    {
        var shell = await Shell(NeedsYouRows.Decision());
        shell.OpenNeedsYouInbox();
        var asked = 0;
        shell.Inbox.CardFocusRequested += (_, _) => asked++;

        var handled = shell.Press("Enter");

        Assert.Equal((true, 1), (handled, asked));
    }, Token);
'''
s=s[:idx]+new+s[idx:]
open(p,'w').write(s)
