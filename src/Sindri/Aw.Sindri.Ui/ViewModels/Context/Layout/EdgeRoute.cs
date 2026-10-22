
namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>
/// The path of one edge on the context map (F3 spec 1 §2, "Edges"): where it starts, then lines, arcs and cubics. Edges inside
/// a band are rounded elbows (arcs of radius <see cref="ElbowRadius"/>); edges between bands are S-curves. Built from fixed
/// coordinates, so the geometry is tested without drawing it.
/// </summary>
/// <param name="Start">The first point (a port on the source node).</param>
/// <param name="Steps">The steps to the last point (a port on the target node).</param>
internal sealed record EdgeRoute(MapPoint Start, IReadOnlyList<RouteStep> Steps)
{
    /// <summary>The radius of an elbow's corner, px.</summary>
    internal const double ElbowRadius = 12;

    // An S-curve's handles are at least this long, so a short climb still leaves the node straight.
    private const double MinHandle = 40;
    private const double MaxPortHandle = 110;
    private const int ArcChords = 8;
    private const int CurveChords = 24;

    /// <summary>Gets the last point.</summary>
    internal MapPoint End => Steps.Count == 0 ? Start : Steps[^1].To;

    /// <summary>A straight line.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Straight(MapPoint from, MapPoint to) => new(from, [new RouteStep(RouteStepKind.Line, to)]);

    /// <summary>The S-curve between two bands: it leaves and arrives vertically, the handles half the climb long (40 px at least).</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Curve(MapPoint from, MapPoint to)
    {
        var sign = to.Y >= from.Y ? 1 : -1;
        var handle = Math.Max(MinHandle, Math.Abs(to.Y - from.Y) / 2) * sign;
        return new EdgeRoute(from, [new RouteStep(RouteStepKind.Cubic, to, Control1: new MapPoint(from.X, from.Y + handle), Control2: new MapPoint(to.X, to.Y - handle))]);
    }

    /// <summary>The cubic between two ports: it leaves each along its side's outward normal, the handles half the distance long, from 40 to 110 px.</summary>
    /// <param name="from">The start port.</param>
    /// <param name="fromSide">The side of the source node the start port is on.</param>
    /// <param name="to">The end port.</param>
    /// <param name="toSide">The side of the target node the end port is on.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Between(MapPoint from, PortSide fromSide, MapPoint to, PortSide toSide)
    {
        var handle = Math.Clamp(Distance(from, to) / 2, MinHandle, MaxPortHandle);
        var (n1, n2) = (Normal(fromSide), Normal(toSide));
        return new EdgeRoute(from, [new RouteStep(RouteStepKind.Cubic, to, Control1: from + (n1 * handle), Control2: to + (n2 * handle))]);
    }

    /// <summary>A line through corner points with each corner rounded by an arc.</summary>
    /// <param name="points">The start, the corners and the end, in order (two or more).</param>
    /// <param name="radius">The corner radius; a short side shrinks it to half that side.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Elbow(IReadOnlyList<MapPoint> points, double radius = ElbowRadius)
    {
        var steps = new List<RouteStep>();
        for (var i = 1; i < points.Count - 1; i++)
        {
            var (before, corner, after) = (points[i - 1], points[i], points[i + 1]);
            var (v1, v2) = (corner - before, after - corner);
            var (l1, l2) = (Math.Sqrt((v1.X * v1.X) + (v1.Y * v1.Y)), Math.Sqrt((v2.X * v2.X) + (v2.Y * v2.Y)));
            var turn = (v1.X * v2.Y) - (v1.Y * v2.X);
            if (l1 == 0 || l2 == 0 || turn == 0)
            {
                steps.Add(new RouteStep(RouteStepKind.Line, corner));
                continue;
            }

            var r = Math.Min(radius, Math.Min(l1, l2) / 2);
            steps.Add(new RouteStep(RouteStepKind.Line, corner - new MapVector(v1.X / l1 * r, v1.Y / l1 * r)));
            steps.Add(new RouteStep(RouteStepKind.Arc, corner + new MapVector(v2.X / l2 * r, v2.Y / l2 * r), r, Clockwise: turn > 0));
        }

        steps.Add(new RouteStep(RouteStepKind.Line, points[^1]));
        return new EdgeRoute(points[0], steps);
    }

    /// <summary>The rounded three-leg route (down or up, across, down or up) between two points of one band: a horizontal run at <paramref name="runY"/>.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="runY">The y of the horizontal leg.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Bus(MapPoint from, MapPoint to, double runY) => Elbow([from, new MapPoint(from.X, runY), new MapPoint(to.X, runY), to]);

    /// <summary>The point half way along the route (a curve at t = 0.5, an elbow at half its length), where the label pill sits.</summary>
    /// <returns>The midpoint.</returns>
    internal MapPoint Midpoint()
    {
        var lengths = new List<(MapPoint From, RouteStep Step, double Length)>();
        var at = Start;
        foreach (var step in Steps)
        {
            if (step.Kind == RouteStepKind.Cubic)
            {
                return Cubic(at, step.Control1, step.Control2, step.To, 0.5);
            }

            lengths.Add((at, step, Distance(at, step.To)));
            at = step.To;
        }

        var half = lengths.Sum(l => l.Length) / 2;
        foreach (var (from, step, length) in lengths)
        {
            if (half <= length && length > 0)
            {
                return from + ((step.To - from) * (half / length));
            }

            half -= length;
        }

        return End;
    }

    /// <summary>The distance from a point to the nearest point of the route, with the arcs and the curve followed as they are drawn.</summary>
    /// <param name="point">A point in the same coordinates as the route.</param>
    /// <returns>The distance, px.</returns>
    internal double DistanceTo(MapPoint point)
    {
        var points = Flatten();
        var best = Distance(point, points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            best = Math.Min(best, DistanceToSegment(point, points[i - 1], points[i]));
        }

        return best;
    }

    /// <summary>The route as a polyline: lines as they are, each arc and the curve cut into short chords.</summary>
    /// <returns>The points from the start to the end.</returns>
    internal IReadOnlyList<MapPoint> Flatten()
    {
        var points = new List<MapPoint> { Start };
        var at = Start;
        foreach (var step in Steps)
        {
            switch (step.Kind)
            {
                case RouteStepKind.Arc:
                    points.AddRange(ArcPoints(at, step));
                    break;
                case RouteStepKind.Cubic:
                    for (var i = 1; i <= CurveChords; i++)
                    {
                        points.Add(Cubic(at, step.Control1, step.Control2, step.To, (double)i / CurveChords));
                    }

                    break;
                case RouteStepKind.Line:
                default:
                    points.Add(step.To);
                    break;
            }

            at = step.To;
        }

        return points;
    }

    // The points of an arc after its start: the centre is on the chord's perpendicular bisector, on the side the turn is toward.
    private static IEnumerable<MapPoint> ArcPoints(MapPoint from, RouteStep step)
    {
        var (chord, r) = (step.To - from, step.Radius);
        var length = Math.Sqrt((chord.X * chord.X) + (chord.Y * chord.Y));
        if (length == 0 || r <= 0)
        {
            yield return step.To;
            yield break;
        }

        var offset = Math.Sqrt(Math.Max(0, (r * r) - (length * length / 4))) * (step.Clockwise ? 1 : -1);
        var centre = new MapPoint(((from.X + step.To.X) / 2) - (chord.Y / length * offset), ((from.Y + step.To.Y) / 2) + (chord.X / length * offset));
        var start = Math.Atan2(from.Y - centre.Y, from.X - centre.X);
        var sweep = Math.Atan2(step.To.Y - centre.Y, step.To.X - centre.X) - start;
        sweep = step.Clockwise ? ((sweep % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI) : -((((-sweep) % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI));
        for (var i = 1; i < ArcChords; i++)
        {
            var angle = start + (sweep * i / ArcChords);
            yield return new MapPoint(centre.X + (r * Math.Cos(angle)), centre.Y + (r * Math.Sin(angle)));
        }

        yield return step.To;
    }

    private static double DistanceToSegment(MapPoint p, MapPoint a, MapPoint b)
    {
        var (ab, ap) = (b - a, p - a);
        var squared = (ab.X * ab.X) + (ab.Y * ab.Y);
        var t = squared == 0 ? 0 : Math.Clamp(((ap.X * ab.X) + (ap.Y * ab.Y)) / squared, 0, 1);
        return Distance(p, new MapPoint(a.X + (ab.X * t), a.Y + (ab.Y * t)));
    }

    private static MapVector Normal(PortSide side) => side switch
    {
        PortSide.Top => new MapVector(0, -1),
        PortSide.Bottom => new MapVector(0, 1),
        PortSide.Left => new MapVector(-1, 0),
        PortSide.Right => new MapVector(1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "not a port side"),
    };

    private static double Distance(MapPoint a, MapPoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static MapPoint Cubic(MapPoint p0, MapPoint p1, MapPoint p2, MapPoint p3, double t)
    {
        var u = 1 - t;
        return new MapPoint(
            (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
            (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y));
    }
}
