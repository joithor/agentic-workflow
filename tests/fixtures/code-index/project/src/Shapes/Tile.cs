namespace Fixture.Shapes;

public class Tile : Rectangle
{
    public Tile(double side) : base(side, side)
    {
    }

    public new string Describe() => "tile " + base.Describe();
}
