namespace Fixture.Shapes;

public sealed class Circle : IShape
{
    private readonly double _radius;

    public Circle(double radius)
    {
        _radius = radius;
    }

    public double Area() => Math.PI * Square(_radius);

    public string Describe() => Labeler.Label("circle", Area());

    public double Radius
    {
        get { return Normalize(_radius); }
    }

    private static double Square(double x) => x * x;

    private static double Normalize(double x) => Math.Abs(x);
}
