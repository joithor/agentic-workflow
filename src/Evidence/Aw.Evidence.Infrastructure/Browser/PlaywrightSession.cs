using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Kernel;
using Microsoft.Playwright;
using Locator = Aw.Evidence.Core.Browser.Locator;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>One page in one Playwright context. Every failure is AW-EVIDENCE-013; disposing closes the context.</summary>
internal sealed class PlaywrightSession(IBrowserContext context, IPage page, ResolvedHost host, bool recording, string browserVersion) : IBrowserSession
{
    private static readonly TimeSpan _signInPage = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan _signInStep = TimeSpan.FromSeconds(60);
    private const float PathPollMs = 50;

    /// <inheritdoc/>
    public string BrowserVersion => browserVersion;

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> GotoAsync(string path, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"goto {path}", () => GotoCheckedAsync(path, timeout), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ClickAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"click {target}", async () =>
            await (await PlaywrightLocators.ResolveCheckedAsync(page, target, timeout).ConfigureAwait(false))
                .ClickAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> FillAsync(Locator target, string value, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"fill {target}", async () =>
            await (await PlaywrightLocators.ResolveCheckedAsync(page, target, timeout).ConfigureAwait(false))
                .FillAsync(value, new() { Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitVisibleAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitAsync(target, WaitForSelectorState.Visible, timeout, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitHiddenAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitAsync(target, WaitForSelectorState.Hidden, timeout, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Polled in the page (as the sign-in's wait to leave the login page is), not through page.WaitForURLAsync: that reads the client's last-known URL and only then subscribes
    /// to the navigated event, both on the .NET side, while events arrive on the connection's reader thread; a navigation
    /// that lands between the two is missed and the wait runs to its timeout. A page-side poll is re-run in each new
    /// document by Playwright itself, so it can't miss a navigation that already happened.
    /// </remarks>
    public ValueTask<Result<Unit>> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"wait for url path {path}", async () =>
        {
            var reached = await page.WaitForFunctionAsync(
                PageScripts.OnPathLoaded, path, new() { PollingInterval = PathPollMs, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
            await reached.DisposeAsync().ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<string>> InputValueAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"read the value of {target}", () =>
            PlaywrightLocators.Resolve(page, target).InputValueAsync(new() { Timeout = PlaywrightLocators.Ms(timeout) }), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Candidate>>> VisibleCandidatesAsync(CancellationToken cancellationToken) =>
        BrowserCall.Run("list the visible candidates", () => PageCapture.VisibleCandidatesAsync(page), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Box>> BoundingBoxAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"measure {target}", () => PageCapture.BoundingBoxAsync(PlaywrightLocators.Resolve(page, target), timeout), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<byte[]>> ScreenshotAsync(CancellationToken cancellationToken) =>
        BrowserCall.Run("screenshot", () => page.ScreenshotAsync(new() { Type = ScreenshotType.Png }), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<PageSnapshot>> LintSnapshotAsync(CancellationToken cancellationToken) =>
        BrowserCall.Run("capture the lint snapshot", () => PageCapture.LintSnapshotAsync(page), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SettleAsync(TimeSpan settle, CancellationToken cancellationToken) =>
        BrowserCall.Run("settle", async () =>
        {
            _ = await page.EvaluateAsync<bool>(PageScripts.FontsReady).ConfigureAwait(false);
            await Task.Delay(settle, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DropFocusAsync(Offset? neutralClick, CancellationToken cancellationToken) =>
        BrowserCall.Run("drop focus", async () =>
        {
            _ = await page.EvaluateAsync<bool>(PageScripts.Blur).ConfigureAwait(false);
            if (neutralClick is not null && !await page.EvaluateAsync<bool>(PageScripts.ControlAt, new { x = neutralClick.X, y = neutralClick.Y }).ConfigureAwait(false))
            {
                await page.Mouse.ClickAsync((float)neutralClick.X, (float)neutralClick.Y).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ResetScrollAsync(CancellationToken cancellationToken) =>
        BrowserCall.Run("reset scroll", async () =>
        {
            _ = await page.EvaluateAsync<bool>(PageScripts.ResetScroll).ConfigureAwait(false);
        }, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SignInAsync(LoginSpec login, Credentials credentials, CancellationToken cancellationToken) =>
        BrowserCall.Run("sign in", async () =>
        {
            _ = await page.GotoAsync(host.Origin, new() { WaitUntil = WaitUntilState.Load, Timeout = PlaywrightLocators.Ms(_signInPage) }).ConfigureAwait(false);
            await FillWhenVisibleAsync(login.EmailSelector, credentials.Email, _signInPage).ConfigureAwait(false);
            if (login.NextSelector is { } next)
            {
                await page.Locator(next).ClickAsync().ConfigureAwait(false);
            }

            await FillWhenVisibleAsync(login.PasswordSelector, credentials.Password, _signInStep).ConfigureAwait(false);
            await page.Locator(login.SubmitSelector).ClickAsync(new() { Force = true }).ConfigureAwait(false);
            var left = await page.WaitForFunctionAsync(
                PageScripts.OffPathLoaded, login.LoginPath, new() { PollingInterval = PathPollMs, Timeout = PlaywrightLocators.Ms(_signInStep) }).ConfigureAwait(false);
            await left.DisposeAsync().ConfigureAwait(false);
            return Unit.Value;
        }, cancellationToken, credentials.Redact);

    /// <inheritdoc/>
    public ValueTask<Result<SessionRecording>> StopRecordingAsync(string tracePath, CancellationToken cancellationToken) =>
        BrowserCall.Run("stop recording", async () =>
        {
            if (!recording)
            {
                return new SessionRecording(Trace: null, Videos: []);
            }

            await context.Tracing.StopAsync(new() { Path = tracePath }).ConfigureAwait(false);
            var video = page.Video;
            await page.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);   // a video file is complete only once its context closes
            return new SessionRecording(tracePath, video is null ? [] : [await video.PathAsync().ConfigureAwait(false)]);
        }, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await context.CloseAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // Already closed (StopRecordingAsync) or the browser is gone; nothing left to release.
        }
    }

    private async Task GotoCheckedAsync(string path, TimeSpan timeout)
    {
        // Defence in depth behind the step guard: a path can't rewrite the host ('//evil.test', '@evil.test').
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new PlaywrightException($"a goto target must be a path on the host: {path}");
        }

        _ = await page.GotoAsync(host.UrlFor(path), new() { WaitUntil = WaitUntilState.Load, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
    }

    private ValueTask<Result<Unit>> WaitAsync(Locator target, WaitForSelectorState state, TimeSpan timeout, CancellationToken cancellationToken) =>
        BrowserCall.Run($"wait for {target} to be {state.ToString().ToLowerInvariant()}", () =>
            PlaywrightLocators.Resolve(page, target).WaitForAsync(new() { State = state, Timeout = PlaywrightLocators.Ms(timeout) }), cancellationToken);

    private async Task FillWhenVisibleAsync(string css, string value, TimeSpan timeout)
    {
        var field = page.Locator(css);
        await field.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = PlaywrightLocators.Ms(timeout) }).ConfigureAwait(false);
        await field.FillAsync(value).ConfigureAwait(false);
    }
}
