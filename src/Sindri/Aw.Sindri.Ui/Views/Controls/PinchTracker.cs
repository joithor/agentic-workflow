namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Turns a pinch's running scale (1 at the start, as a touch pinch reports it) into the factor for each step, and a
/// trackpad magnify's increment (macOS reports the change since the last event) into a factor. Pure, so the stage's wiring stays thin.
/// </summary>
internal sealed class PinchTracker
{
    private double _last = 1;

    /// <summary>The factor for a trackpad magnify event.</summary>
    /// <param name="magnification">The increment since the last event; above 0 spreads the fingers (zooms in).</param>
    /// <returns>The scale factor, never below a small positive number so a hard pinch cannot flip or zero the view.</returns>
    internal static double MagnifyFactor(double magnification) => Math.Max(0.05, 1 + magnification);

    /// <summary>The factor between the previous pinch event and this one.</summary>
    /// <param name="scale">The pinch's scale since it began.</param>
    /// <returns>The factor to zoom by now; 1 when the scale is not usable.</returns>
    internal double Step(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return 1;
        }

        var factor = scale / _last;
        _last = scale;
        return factor;
    }

    /// <summary>Starts over for the next pinch.</summary>
    internal void End() => _last = 1;
}
