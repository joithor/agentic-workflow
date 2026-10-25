namespace Fixture.Shapes;

public partial class Rectangle : IShape, IEquatable<Rectangle>
{
    private readonly Dimensions _size;

    public Rectangle(double width, double height)
    {
        _size = new Dimensions(width, height);
    }

    public double Area() => _size.Width * _size.Height;

    public bool Equals(Rectangle? other) => other is not null && Area() == other.Area();

    public readonly struct Dimensions
    {
        public Dimensions(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }

        public double Diagonal() => Math.Sqrt(Width * Width + Height * Height);
    }
}
