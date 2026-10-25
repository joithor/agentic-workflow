using Fixture.Calc;
using Fixture.Shapes;

namespace Fixture.App;

public static class Program
{
    public static int Main()
    {
        var shapes = new List<IShape> { new Circle(1.5), new Rectangle(2, 3), new Tile(4) };
        var registry = new Registry<IShape>();
        foreach (var shape in shapes)
        {
            registry.Register(shape);
            Console.WriteLine(shape.ToSummary());
        }

        var calculator = new Calculator();
        var total = calculator.Sum(1, 2, 3) + calculator.Product(2, 3);
        var report = Pipeline.Run(shapes, calculator);
        Console.WriteLine(report);
        return total > 0 ? 0 : 1;
    }

    private static string Describe(IShape? shape) => shape?.Describe() ?? "none";
}
