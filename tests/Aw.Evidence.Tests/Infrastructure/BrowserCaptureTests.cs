using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Infrastructure.Guard;
using Aw.Evidence.Infrastructure.Images;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Heavy)]
[Collection(SharedBrowser.Name)]
public sealed class BrowserCaptureTests(BrowserFixture fixture) : IDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private readonly TempDirectory _runs = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_screenshot_is_a_png_of_the_viewport()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);

        var image = new PngCodec().Decode((await session.ScreenshotAsync(Token)).Value).Value;

        Assert.Equal((1280, 800), (image.Width, image.Height));
        Assert.False(string.IsNullOrEmpty(session.BrowserVersion));
    }

    [Fact]
    public async Task A_recorded_session_leaves_a_trace_archive_and_a_video()
    {
        var session = await fixture.OpenAsync(_runs.Path);
        _ = await session.GotoAsync("/", _wait, Token);

        var recording = (await session.StopRecordingAsync(_runs.Combine("desktop-trace.zip"), Token)).Value;
        await session.DisposeAsync();

        Assert.Equal(_runs.Combine("desktop-trace.zip"), recording.Trace);
        Assert.Equal("PK"u8.ToArray(), (await File.ReadAllBytesAsync(recording.Trace!, Token))[..2]);   // why: a recording session always has a trace
        var video = Assert.Single(recording.Videos);
        Assert.StartsWith(_runs.Path, video, StringComparison.Ordinal);
        Assert.True(new FileInfo(video).Length > 0);
    }

    [Fact]
    public async Task The_lint_snapshot_reads_boxes_styles_and_hover_and_focus_rules()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);

        var snapshot = (await session.LintSnapshotAsync(Token)).Value;

        Assert.Equal(new ViewportSize(1280, 800), snapshot.Viewport);
        var styled = Assert.Single(snapshot.Elements, e => e.Text == "Open");
        Assert.True(styled.HasHoverState && styled.HasFocusState);
        Assert.True(styled.Rect.W > 0 && styled.Rect.H > 0);
        Assert.NotNull(styled.Style.Color);
        var plain = Assert.Single(snapshot.Elements, e => e.Selector == "button#plain");
        Assert.False(plain.HasHoverState || plain.HasFocusState);
    }

    [Fact]
    public async Task Scroll_reset_brings_scrolled_content_back_and_boxes_have_two_decimals()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/tall", _wait, Token);
        var top = Locator.ByTestId(TestId.From("top"));
        Assert.True((await session.SettleAsync(TimeSpan.FromMilliseconds(100), Token)).IsSuccess);

        var scrolled = (await session.BoundingBoxAsync(top, _wait, Token)).Value;
        Assert.True((await session.ResetScrollAsync(Token)).IsSuccess);
        var reset = (await session.BoundingBoxAsync(top, _wait, Token)).Value;

        Assert.True(scrolled.Y < 0, "the page loads scrolled");
        Assert.True(reset.Y >= 0, "the reset scrolls back");
        Assert.Equal(Math.Round(reset.W, 2, MidpointRounding.AwayFromZero), reset.W);
    }

    [Fact]
    public async Task Dropping_focus_clicks_an_empty_spot_but_never_a_control()
    {
        await using var session = await fixture.OpenAsync();
        _ = await session.GotoAsync("/", _wait, Token);
        var corner = (await session.BoundingBoxAsync(Locator.ByTestId(TestId.From("corner")), _wait, Token)).Value;

        Assert.True((await session.DropFocusAsync(new Offset(corner.X + 2, corner.Y + 2), Token)).IsSuccess);
        Assert.True((await session.WaitHiddenAsync(Locator.ByText("Corner clicked"), TimeSpan.FromMilliseconds(300), Token)).IsSuccess, "a control is never clicked");
        Assert.True((await session.DropFocusAsync(new Offset(1200, 700), Token)).IsSuccess);
        Assert.True((await session.WaitVisibleAsync(Locator.ByText("Neutral clicked"), _wait, Token)).IsSuccess, "an empty spot is clicked");
    }

    [Fact]
    public async Task Sign_in_fills_a_two_step_form_and_waits_to_leave_the_login_page()
    {
        using var app = new LocalHttpServer(FixtureSite.SignInApp());
        await using var session = await fixture.OpenAsync(origin: app.Origin);
        var login = new LoginSpec("QA_EMAIL", "QA_PASSWORD", "#email", "#password", "#submit", nextSelector: "#next");
        var credentials = Credentials.Read(new EnvironmentSecrets(n => n == "QA_EMAIL" ? "qa@example.com" : "s3cret"), "QA_EMAIL", "QA_PASSWORD").Value;

        var signedIn = await session.SignInAsync(login, credentials, Token);

        Assert.True(signedIn.IsSuccess, signedIn.IsError ? signedIn.Error.Message : null);
        Assert.True((await session.WaitForPathAsync("/home", _wait, Token)).IsSuccess);
    }

    public void Dispose() => _runs.Dispose();
}
