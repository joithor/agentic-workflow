using Fixture.Calc;
using Fixture.Shapes;

namespace Fixture.App;

public static class Pipeline
{
    public static string Run(IReadOnlyList<IShape> shapes, Calculator calculator)
    {
        string Line(IShape shape) => shape.Describe();

        var areas = shapes.Select(shape => shape.Area()).ToArray();
        var lines = shapes.Select(Line).ToArray();
        var sum = calculator.Total(areas.Select(a => (int)a).ToArray());
        return string.Join("; ", lines) + " total " + sum;
    }
}
