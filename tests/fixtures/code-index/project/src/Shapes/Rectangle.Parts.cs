namespace Fixture.Shapes;

public partial class Rectangle
{
    public string Describe() => Labeler.Label("rectangle", Area());

    public bool IsSquare() => Math.Abs(_size.Width - _size.Height) < 0.0001;
}
