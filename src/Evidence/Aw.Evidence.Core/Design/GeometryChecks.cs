using System.Globalization;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Design;

/// <summary>Compares geometry expectations with measured boxes. A missing element fails; it is never skipped.</summary>
public static class GeometryChecks
{
    /// <summary>Checks every expectation.</summary>
    /// <param name="expectations">The expectations.</param>
    /// <param name="measure">The measured box for a selector, or null when the element is missing.</param>
    /// <returns>One check per expectation, in order.</returns>
    public static IReadOnlyList<BoxCheck> Check(IReadOnlyList<BoxExpectation> expectations, Func<ElementSelector, Box?> measure) =>
        [.. expectations.Select(e => CheckOne(e, measure))];

    /// <summary>Every selector the expectations measure (targets and their origins).</summary>
    /// <param name="expectations">The expectations.</param>
    /// <returns>The selectors, in order, repeats included.</returns>
    public static IEnumerable<ElementSelector> SelectorsOf(IReadOnlyList<BoxExpectation> expectations) =>
        expectations.SelectMany(static e => e.RelativeTo is null ? [e.Target] : new[] { e.Target, e.RelativeTo });

    private static BoxCheck CheckOne(BoxExpectation e, Func<ElementSelector, Box?> measure)
    {
        var tolerance = e.Tolerance ?? 0;
        var relativeTo = e.RelativeTo?.Key;
        var box = measure(e.Target);
        var origin = e.RelativeTo is null ? new Box(0, 0, 0, 0) : measure(e.RelativeTo);
        if (box is null || origin is null)
        {
            var missing = box is null ? e.Target.Key : relativeTo;
            return new(e.Target.Key, relativeTo, e.Expected, Actual: null, tolerance, Pass: false, $"{missing} not found");
        }

        var actual = new MeasuredGeometry(box.W, box.H, box.X - origin.X, box.Y - origin.Y);
        string[] misses =
        [
            .. Miss("w", actual.W, e.Expected.W, tolerance), .. Miss("h", actual.H, e.Expected.H, tolerance),
            .. Miss("x", actual.X, e.Expected.X, tolerance), .. Miss("y", actual.Y, e.Expected.Y, tolerance),
        ];
        return new(e.Target.Key, relativeTo, e.Expected, actual, tolerance, misses.Length == 0, misses.Length == 0 ? null : string.Join(", ", misses));
    }

    private static string[] Miss(string axis, double actual, double? expected, double tolerance) =>
        expected is { } want && Math.Abs(actual - want) > tolerance
            ? [string.Create(CultureInfo.InvariantCulture, $"{axis} {actual} != {want}")]
            : [];
}
