using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;

namespace Aw.Evidence.Features.Parity;

/// <summary>
/// Brings one frame's state about (wait, steps without repair, form values), settles the page (fonts, focus, scroll),
/// measures the anchor and the expected boxes, and hands the region to the comparison.
/// </summary>
internal sealed class FrameCapture(StepRunner runner, FrameComparison comparison)
{
    private static readonly TimeSpan _stepTimeoutCap = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _measureTimeoutCap = TimeSpan.FromSeconds(5);

    internal async ValueTask<Result<ParityFrame>> MeasureAsync(IBrowserSession session, ParityContext context, DesignFrame frame, CancellationToken cancellationToken)
    {
        var reached = await ReachStateAsync(session, context.Manifest, frame, cancellationToken).ConfigureAwait(false);
        var settled = reached.IsError ? reached : await SettleAsync(session, context.Manifest, cancellationToken).ConfigureAwait(false);
        if (settled.IsError)
        {
            return settled.Error;
        }

        var boxes = await MeasureBoxesAsync(session, context.Manifest, frame, cancellationToken).ConfigureAwait(false);
        if (boxes.GetValueOrDefault(frame.Anchor.Key) is not { } anchor)
        {
            return Failed(frame, $"anchor {frame.Anchor.Key} not found");
        }

        var region = ParityRegions.Implementation(anchor, frame.DesignRegion, frame.AnchorOffset ?? Offset.Zero);
        if (ParityRegions.Shortfall(region, context.Manifest.Viewport) is { } need)
        {
            return Failed(frame, $"the region needs a viewport of at least {need.Describe()}, manifest viewport is {context.Manifest.Viewport.Describe()}");
        }

        var checks = GeometryChecks.Check(frame.ExpectBoxes, s => boxes.GetValueOrDefault(s.Key));
        var measured = await comparison.CompareAsync(session, context, new FrameRegion(frame, anchor, region), cancellationToken).ConfigureAwait(false);
        return measured.Map(m => m with { BoxChecks = checks }).Map(m => m.ToFrame(context.Manifest));
    }

    private async ValueTask<Result<Unit>> ReachStateAsync(IBrowserSession session, DesignManifest manifest, DesignFrame frame, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(manifest.TimeoutMs);
        if (frame.WaitFor is { } waitFor && (await session.WaitVisibleAsync(Locator.For(waitFor), timeout, cancellationToken).ConfigureAwait(false)).IsError)
        {
            return Failed(frame, $"waitFor {waitFor.Key} never became visible");
        }

        var options = StepRunOptions.Parity(timeout < _stepTimeoutCap ? timeout : _stepTimeoutCap);
        for (var i = 0; i < frame.Steps.Count; i++)
        {
            var step = frame.Steps[i];
            var status = await runner.RunAsync(session, step, options, new InvocationLog(), cancellationToken).ConfigureAwait(false);
            if (status != StepStatus.Passed)
            {
                return Failed(frame, string.Create(CultureInfo.InvariantCulture, $"step {i + 1} ({step.ActionName} {step.Target}) {StatusName(status)}"));
            }
        }

        foreach (var value in frame.FormValues)
        {
            var filled = await session.FillAsync(Locator.For(value), value.Value, timeout, cancellationToken).ConfigureAwait(false);
            if (filled.IsError)
            {
                return filled.Error;
            }
        }

        return Unit.Value;
    }

    // Everything that spoils a capture: late fonts, a focus ring, a half-faded popover, a scrolled container.
    private static async ValueTask<Result<Unit>> SettleAsync(IBrowserSession session, DesignManifest manifest, CancellationToken cancellationToken)
    {
        var settle = TimeSpan.FromMilliseconds(manifest.SettleMs);
        var first = await session.SettleAsync(settle, cancellationToken).ConfigureAwait(false);
        var focus = first.IsError ? first : await session.DropFocusAsync(manifest.NeutralClickPoint(), cancellationToken).ConfigureAwait(false);
        var scroll = focus.IsError ? focus : await session.ResetScrollAsync(cancellationToken).ConfigureAwait(false);
        return scroll.IsError ? scroll : await session.SettleAsync(settle, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Dictionary<string, Box>> MeasureBoxesAsync(IBrowserSession session, DesignManifest manifest, DesignFrame frame, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Min(manifest.TimeoutMs, _measureTimeoutCap.TotalMilliseconds));
        var boxes = new Dictionary<string, Box>(StringComparer.Ordinal);
        foreach (var selector in GeometryChecks.SelectorsOf(frame.ExpectBoxes).Prepend(frame.Anchor).DistinctBy(static s => s.Key, StringComparer.Ordinal))
        {
            var box = await session.BoundingBoxAsync(Locator.For(selector), timeout, cancellationToken).ConfigureAwait(false);
            if (box.IsSuccess)
            {
                boxes[selector.Key] = box.Value;
            }
        }

        return boxes;
    }

    private static string StatusName(StepStatus status) => status switch
    {
        StepStatus.Passed => "passed",
        StepStatus.Failed => "failed",
        StepStatus.Broken => "broken",
        _ => "unknown",
    };

    private static Error Failed(DesignFrame frame, string what) => EvidenceErrors.RunFailed.With($"frame {frame.Name}: {what}");
}
