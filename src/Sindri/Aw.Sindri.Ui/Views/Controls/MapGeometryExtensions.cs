using Avalonia;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The map's plain geometry (view model side) in Avalonia's types, where the views draw and hit-test.</summary>
internal static class MapGeometryExtensions
{
    /// <summary>A map point as an Avalonia point.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The same place.</returns>
    internal static Point ToAvalonia(this MapPoint point) => new(point.X, point.Y);

    /// <summary>An Avalonia point as a map point.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The same place.</returns>
    internal static MapPoint ToMap(this Point point) => new(point.X, point.Y);

    /// <summary>A map rectangle as an Avalonia rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The same place.</returns>
    internal static Rect ToAvalonia(this MapRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>The route as a geometry to stroke.</summary>
    /// <param name="route">The route.</param>
    /// <returns>An open stream geometry.</returns>
    internal static StreamGeometry ToGeometry(this EdgeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(route.Start.ToAvalonia(), isFilled: false);
            foreach (var step in route.Steps)
            {
                switch (step.Kind)
                {
                    case RouteStepKind.Arc:
                        context.ArcTo(step.To.ToAvalonia(), new Size(step.Radius, step.Radius), 0, isLargeArc: false, step.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                        break;
                    case RouteStepKind.Cubic:
                        context.CubicBezierTo(step.Control1.ToAvalonia(), step.Control2.ToAvalonia(), step.To.ToAvalonia());
                        break;
                    case RouteStepKind.Line:
                    default:
                        context.LineTo(step.To.ToAvalonia());
                        break;
                }
            }

            context.EndFigure(isClosed: false);
        }

        return geometry;
    }
}
