namespace Fixture.Shapes;

public enum ShapeKind
{
    Round,
    Angular,
}

public record ShapeInfo(string Name, double Area)
{
    public string Title() => Name.ToUpperInvariant();
}
