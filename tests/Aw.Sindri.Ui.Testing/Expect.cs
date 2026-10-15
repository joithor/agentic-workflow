using System.Runtime.CompilerServices;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The checks the contract bases make; a failure is a <see cref="ContractViolationException"/> naming the expression.</summary>
internal static class Expect
{
    public static void True(bool condition, [CallerArgumentExpression(nameof(condition))] string expression = "")
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: expected {expression}");
        }
    }

    public static void Equal<T>(T expected, T actual, [CallerArgumentExpression(nameof(actual))] string expression = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new ContractViolationException($"contract: {expression} was {actual}, expected {expected}");
        }
    }

    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T>? actual, [CallerArgumentExpression(nameof(actual))] string expression = "")
    {
        if (actual is null || !expected.SequenceEqual(actual))
        {
            throw new ContractViolationException($"contract: {expression} was [{string.Join(", ", actual ?? [])}], expected [{string.Join(", ", expected)}]");
        }
    }
}
