namespace Fixture.Shapes;

public static class Labeler
{
    public static string Label(string name) => Label(name, 0);

    public static string Label(string name, double value) => name + ":" + Format(value);

    public static string Format(int value) => value.ToString();

    public static string Format(double value) => value.ToString("F2");
}
