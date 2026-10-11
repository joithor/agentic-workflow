using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;

namespace Aw.Evidence.Features.Parity;

/// <summary>
/// One signed-in browser session over the manifest's route: sign in when the manifest says so, open the route, wait
/// for the ready element (a text-absent check straight after navigation would pass on a loading spinner), then measure
/// each frame in order. Any failure stops the run.
/// </summary>
internal sealed class ParityRun(IBrowser browser, FrameCapture capture, ISecretsSource settings)
{
    internal const string UserAgentVariable = "UI_EVIDENCE_USER_AGENT";
    private static readonly TimeSpan _navigationTimeout = TimeSpan.FromSeconds(120);

    internal async ValueTask<Result<IReadOnlyList<ParityFrame>>> RunAsync(ParityContext context, CancellationToken cancellationToken)
    {
        var options = new BrowserSessionOptions(context.Host, context.Manifest.Viewport, RecordDirectory: null, settings.Get(UserAgentVariable));
        var opened = await browser.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            var ready = await OpenRouteAsync(opened.Value, context, cancellationToken).ConfigureAwait(false);
            return ready.IsError ? ready.Error : await FramesAsync(opened.Value, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<Unit>> OpenRouteAsync(IBrowserSession session, ParityContext context, CancellationToken cancellationToken)
    {
        var manifest = context.Manifest;
        if (manifest.Login is { } login && context.Credentials is { } credentials)
        {
            var signedIn = await session.SignInAsync(login, credentials, cancellationToken).ConfigureAwait(false);
            if (signedIn.IsError)
            {
                return signedIn.Error;
            }
        }

        var opened = await session.GotoAsync(manifest.Route, _navigationTimeout, cancellationToken).ConfigureAwait(false);
        return opened.IsError
            ? opened.Error
            : await session.WaitVisibleAsync(Locator.For(manifest.Ready), TimeSpan.FromMilliseconds(manifest.TimeoutMs), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<ParityFrame>>> FramesAsync(IBrowserSession session, ParityContext context, CancellationToken cancellationToken)
    {
        var frames = new List<ParityFrame>();
        foreach (var frame in context.Manifest.Frames)
        {
            var measured = await capture.MeasureAsync(session, context, frame, cancellationToken).ConfigureAwait(false);
            if (measured.IsError)
            {
                return measured.Error;
            }

            frames.Add(measured.Value);
        }

        return frames;
    }
}
