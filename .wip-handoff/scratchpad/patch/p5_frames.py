root='/home/thor/repos/aw-wt/sindri-ui-needs-you-2/tests/Aw.Sindri.Ui.Tests/'
p=root+'Views/NeedsYouFramesTests.cs'
s=open(p).read()
idx=s.rstrip().rfind('}')
new='''
    // F3 needs-you slice 2: the action bar under the card (prototype #pending, #confirm, #sendback, #approved).
    private void ServeDesk()
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"));
    }

    private async Task<(ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> DeskInbox(double width = 1440)
    {
        ServeDesk();
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: width, height: 800);
        shell.OpenNeedsYouInbox();
        Settle(window);
        await shell.Inbox.Bar.Loaded;
        Settle(window);
        return (window, shell);
    }

    [Theory]
    [InlineData(1440, "needs-you-bar-1440")]
    [InlineData(900, "needs-you-bar-900")]
    public Task The_bar_draws_approve_send_back_and_the_chip_under_the_card(int width, string frame) => OnUi(async () =>
    {
        var (window, _) = await DeskInbox(width);

        Assert.True(Visible(window, "NeedsYouActionBar"));
        Assert.Equal("Approve Try the engine", Avalonia.Automation.AutomationProperties.GetName(Named<Button>(window, "NeedsYouApprove")));
        Assert.Equal("Send back Try the engine", Avalonia.Automation.AutomationProperties.GetName(Named<Button>(window, "NeedsYouSendBack")));
        Assert.True(Visible(window, "NeedsYouEditChip"));
        Assert.False(Named<Control>(window, "NeedsYouEditChip").Focusable);
        Assert.True(Named<Control>(window, "NeedsYouActionBar").Bounds.Bottom <= window.ClientSize.Height, "the bar is cut off");
        Assert.False(Visible(window, "NeedsYouCardControls"));
        Frame(window, frame);
    });

    [Fact]
    public Task A_reports_a_short_card_at_once_and_the_done_card_replaces_it() => OnUi(async () =>
    {
        var (window, shell) = await DeskInbox();

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        for (var i = 0; i < 400 && !shell.Inbox.IsDone; i++)
        {
            await Task.Delay(5, Token);
        }
        Settle(window);

        Assert.Equal("approve", Server.CompleteCalls.Single().Body.Outcome);
        Assert.True(Visible(window, "NeedsYouDone"));
        Assert.False(Visible(window, "NeedsYouCard"));
        Assert.Equal("Approved: Try the engine", Named<TextBlock>(window, "NeedsYouDoneTitle").Text);
        Frame(window, "needs-you-approved");
    });

    [Fact]
    public Task D_opens_the_reason_box_Ctrl_Enter_sends_it_and_Esc_closes_it_before_leaving_the_inbox() => OnUi(async () =>
    {
        var (window, shell) = await DeskInbox();

        window.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None);
        Settle(window);
        Assert.True(Visible(window, "NeedsYouReason"));
        Frame(window, "needs-you-send-back");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        Assert.True(shell.IsNeedsYouInbox);
        Assert.False(Visible(window, "NeedsYouReason"));

        window.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None);
        Settle(window);
        shell.Inbox.Bar.ReasonText = "cut the scope";
        Named<TextBox>(window, "NeedsYouReason").Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        for (var i = 0; i < 400 && Server.CompleteCalls.Count == 0; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal("revise", Server.CompleteCalls.Single().Body.Outcome);
    });

    [Fact]
    public Task Escape_with_nothing_open_in_the_bar_still_leaves_the_inbox() => OnUi(async () =>
    {
        var (window, shell) = await DeskInbox();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.False(shell.IsNeedsYouInbox);
    });
'''
s=s[:idx]+new+s[idx:]
open(p,'w').write(s)
