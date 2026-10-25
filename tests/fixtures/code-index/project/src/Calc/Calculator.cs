namespace Fixture.Calc;

public class Calculator
{
    public int Sum(int a, int b) => a + b;

    public int Sum(int a, int b, int c) => Sum(Sum(a, b), c);

    public int Product(int a, int b) => a * b;

    public int Total(int[] values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total = this.Sum(total, value);
        }

        return Scale(total);
    }

    private static int Scale(int value)
    {
        int Doubled(int x) => x * 2;

        Func<int, int> twice = x => Doubled(x);
        return twice(value);
    }
}
